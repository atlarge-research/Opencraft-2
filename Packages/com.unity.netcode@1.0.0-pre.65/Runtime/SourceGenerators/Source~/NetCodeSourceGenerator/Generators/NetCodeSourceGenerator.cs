using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Unity.NetCode.Generators
{
    public class GlobalOptions
    {
        /// <summary>
        /// Override the current project path. Used by the generator to flush logs or lookup files.
        /// </summary>
        public const string ProjectPath = "unity.netcode.sourcegen.projectpath";
        /// <summary>
        /// Override the output folder where the generator flush logs and generated files.
        /// </summary>
        public const string OutputPath = "unity.netcode.sourcegen.outputfolder";
        /// <summary>
        /// Skip validation of missing assmebly references. Mostly used for testing.
        /// </summary>
        public const string DisableRerencesChecks = "unity.netcode.sourcege.disable_references_checks";
        /// <summary>
        /// Enable/Disable support for passing custom templates using additional files. Mostly for testing
        /// </summary>
        public const string TemplateFromAdditionalFiles = "unity.netcode.sourcege.templates_from_additional_files";
        /// <summary>
        /// Enable/Disable writing generated code to output folder
        /// </summary>
        public const string WriteFilesToDisk = "unity.netcode.sourcege.write_files_to_disk";
    }

    /// <summary>
    /// Parse the syntax tree using <see cref="NetCodeSyntaxReceiver"/> and generate for Rpc, Commands and Ghost
    /// serialization code.
    /// Must be stateless and immutable. Can be called from multiple thread or the instance reused
    /// </summary>
    [Generator]
    public class NetCodeSourceGenerator : ISourceGenerator
    {
        internal struct Candidates
        {
            public List<SyntaxNode> Components;
            public List<SyntaxNode> Rpcs;
            public List<SyntaxNode> Commands;
            public List<SyntaxNode> Inputs;
            public List<SyntaxNode> Variants;
        }

        public const string NETCODE_ADDITIONAL_FILE = ".NetCodeSourceGenerator.additionalfile";

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new NetCodeSyntaxReceiver());
            //Initialize the profile here also take in account the internal Unity compilation time not
            //stritcly related to the source generators. This is useful metric to have, since we can then how
            //much we accounts (in %) in respect to the total compilation time.
            Profiler.Initialize();
        }

        static bool ShouldRunGenerator(GeneratorExecutionContext executionContext)
        {
            //Skip running if no references to netcode are passed to the compilation
            return executionContext.Compilation.Assembly.Name.StartsWith("Unity.NetCode", StringComparison.Ordinal) ||
                   executionContext.Compilation.ReferencedAssemblyNames.Any(r=>
                       r.Name.Equals("Unity.NetCode", StringComparison.Ordinal) ||
                       r.Name.Equals("Unity.NetCode.ref", StringComparison.Ordinal));
        }

        /// <summary>
        /// Main entry point called from Roslyn, after the syntax analysis has been completed.
        /// At this point we should have collected all the candidates
        /// </summary>
        /// <param name="executionContext"></param>
        public void Execute(GeneratorExecutionContext executionContext)
        {
            executionContext.CancellationToken.ThrowIfCancellationRequested();

            if (!ShouldRunGenerator(executionContext))
                return;

            Helpers.SetupContext(executionContext);
            var diagnostic = new DiagnosticReporter(executionContext);
            diagnostic.LogInfo($"Begin Processing assembly {executionContext.Compilation.AssemblyName} candidates");
            try
            {
                Generate(executionContext, diagnostic);
            }
            catch (Exception e)
            {
                diagnostic.LogException(e);
            }
            diagnostic.LogInfo($"End Processing assembly {executionContext.Compilation.AssemblyName} candidates");
            diagnostic.LogInfo(Profiler.PrintStats());
        }

        private static void Generate(GeneratorExecutionContext executionContext, IDiagnosticReporter diagnostic)
        {
            //Try to dispatch any unknown candidates to the right array by checking what interface the struct is implementing
            var receiver = (NetCodeSyntaxReceiver)executionContext.SyntaxReceiver;
            var candidates = ResolveCandidates(executionContext, receiver, diagnostic);
            var totalCandidates = candidates.Rpcs.Count + candidates.Commands.Count + candidates.Components.Count + candidates.Variants.Count + candidates.Inputs.Count;
            if (totalCandidates == 0)
                return;

            //Initialize template registry and register custom user type definitions
            var typeRegistry = new TypeRegistry(DefaultTypes.Registry);
            List<TypeRegistryEntry> customUserTypes;
            using (new Profiler.Auto("LoadRegistryAndOverrides"))
            {
                customUserTypes = UserDefinedTemplateRegistryParser.ParseTemplates(executionContext, diagnostic);
                typeRegistry.AddRange(customUserTypes);
            }
            var templateFileProvider = new TemplateFileProvider(diagnostic);
            //Additional files always provides the extra templates in 2021.2 and newer. The templates files must end with .netcode.additionalfile extensions.
            templateFileProvider.AddAdditionalTemplates(executionContext.AdditionalFiles, customUserTypes);
            templateFileProvider.PerformAdditionalTypeRegistryValidation(customUserTypes);
            if (!Helpers.SupportTemplateFromAdditionalFiles)
            {
                //template path are resolved dynamically using the current project path.
                var pathResolver = new PathResolver(Helpers.ProjectPath);
                pathResolver.LoadManifestMapping();
                templateFileProvider.pathResolver = pathResolver;
            }
            var codeGenerationContext = new CodeGenerator.Context(typeRegistry, templateFileProvider, diagnostic, executionContext, executionContext.Compilation.AssemblyName);
            // The ghost,commands and rpcs generation start here. Just loop through all the semantic models, check
            // the necessary conditions and pass the extract TypeInformation to our custom code generation system
            // that will build the necessary source code.
            using (new Profiler.Auto("Generate"))
            {
                // Generate command data wrapper for input data and the CopyToBuffer/CopyFromBuffer systems
                using(new Profiler.Auto("InputGeneration"))
                    InputFactory.Generate(candidates.Inputs, codeGenerationContext, executionContext);
                //Generate serializers for components and buffers
                using (new Profiler.Auto("ComponentGeneration"))
                    ComponentFactory.Generate(candidates.Components, candidates.Variants, codeGenerationContext);
                // Generate serializers for rpcs and commands
                using(new Profiler.Auto("CommandsGeneration"))
                    CommandFactory.Generate(candidates.Commands, codeGenerationContext);
                using(new Profiler.Auto("RpcGeneration"))
                    RpcFactory.Generate(candidates.Rpcs, codeGenerationContext);
            }
            if (codeGenerationContext.batch.Count > 0)
            {
                executionContext.AnalyzerConfigOptions.GlobalOptions.TryGetValue(GlobalOptions.DisableRerencesChecks, out var disableReferencesChecks);
                if (string.IsNullOrEmpty(disableReferencesChecks))
                {
                    //Make sure the assembly has the right references and treat them as a fatal error
                    var missingReferences = new HashSet<string>{"Unity.Collections", "Unity.Burst", "Unity.Mathematics"};
                    foreach (var r in executionContext.Compilation.ReferencedAssemblyNames)
                        missingReferences.Remove(r.Name);
                    if (missingReferences.Count > 0)
                    {
                        codeGenerationContext.diagnostic.LogError(
                            $"Assembly {executionContext.Compilation.AssemblyName} contains NetCode replicated types. The serialization code will use " +
                            $"burst, collections, mathematics and network data streams but the assembly does not have references to: {string.Join(",", missingReferences)}. " +
                            $"Please add the missing references in the asmdef for {executionContext.Compilation.AssemblyName}.");
                    }
                }
            }
            AddGeneratedSources(executionContext, codeGenerationContext);
        }

        /// <summary>
        /// Map ambigous syntax nodes to code-generation type candidates.
        /// </summary>
        /// <param name="executionContext"></param>
        /// <param name="receiver"></param>
        /// <param name="diagnostic"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private static Candidates ResolveCandidates(GeneratorExecutionContext executionContext, NetCodeSyntaxReceiver receiver, IDiagnosticReporter diagnostic)
        {
            var candidates = new Candidates
            {
                Components = new List<SyntaxNode>(),
                Rpcs = new List<SyntaxNode>(),
                Commands = new List<SyntaxNode>(),
                Inputs = new List<SyntaxNode>(),
                Variants = receiver.Variants
            };

            foreach (var candidate in receiver.Candidates)
            {
                executionContext.CancellationToken.ThrowIfCancellationRequested();

                var symbolModel = executionContext.Compilation.GetSemanticModel(candidate.SyntaxTree);
                var candidateSymbol = symbolModel.GetDeclaredSymbol(candidate) as ITypeSymbol;
                var allComponentTypes = Roslyn.Extensions.GetAllComponentType(candidateSymbol).ToArray();
                //No valid/known interfaces
                if (allComponentTypes.Length == 0)
                    continue;

                //The struct is implementing more than one valid interface. Report the error/warning and skip the code-generation
                if (allComponentTypes.Length > 1)
                {
                    diagnostic.LogError(
                        $"struct {Roslyn.Extensions.GetFullTypeName(candidateSymbol)} cannot implement {string.Join(",", allComponentTypes)} interfaces at the same time",
                        candidateSymbol?.Locations[0]);
                    continue;
                }
                switch (allComponentTypes[0])
                {
                    case ComponentType.Unknown:
                        break;
                    case ComponentType.Component:
                        candidates.Components.Add(candidate);
                        break;
                    case ComponentType.HybridComponent:
                        candidates.Components.Add(candidate);
                        break;
                    case ComponentType.Buffer:
                        candidates.Components.Add(candidate);
                        break;
                    case ComponentType.Rpc:
                        candidates.Rpcs.Add(candidate);
                        break;
                    case ComponentType.CommandData:
                        candidates.Commands.Add(candidate);
                        candidates.Components.Add(candidate);
                        break;
                    case ComponentType.Input:
                        candidates.Inputs.Add(candidate);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return candidates;
        }

        /// <summary>
        /// Add the generated source files to the current compilation and flush everything on disk (if enabled)
        /// </summary>
        /// <param name="executionContext"></param>
        /// <param name="codeGenContext"></param>
        private static void AddGeneratedSources(GeneratorExecutionContext executionContext, CodeGenerator.Context codeGenContext)
        {
            //There are a couple of problem by storing the files in the Temp folder: all generated files are lost when the editor quit.
            //Another solution would be to store them in another persistent folder, like Library or Assets/NetCodeGenerated~.
            //In both cases there is no easy way from the source generator to cleanup any deleted or removed assemblies or assemblies who don't have
            //any ghost / rpc to generate anymore.
            // Our current proposed solution:
            // 1- We store generated files for each assembly in seperate folders. The folder is deleted and repopulated any time that assembly is rebuit.
            //    This way we are guarantee that the generated files are always in sync with the assembly contents after any update.
            // 2- When the editor is going to quit, we make a copy the Temp/NetCodeGenerated folder in the Library/NetCodeGenerated_Backup.
            // 3- When the editor is reopend, if the Temp/NetCodeGenerated directory does not exists, all the files are copied back. Otherwise,
            //    only the subset of directory that are not present in Temp but in the backup are restored (we can eventually check the date)
            //
            // An extra pass that check for all the assembly in the compilation pipeline and remove any not present anymore
            // is done by the CodeGenService in the Unity.NetCode.Editor, that will take care of that.
            //
            // USE CASES TAKEN INTO EXAMS:
            //- Editor close/re-open, no changes. It work as expected.
            //- Editor crash/re-open. Temp is never deleted in that case so should be ok
            //- When checkout/update to a newer version
            //   a) With editor closed: When the editor is opened again, if something in code changed, a first compilation pass is always done before
            //                          the domain reload. That means is safe to assume that whatever has been regenerated by the deps tree
            //                          is the most up-to-date version of the assembly generated files. Only the folders that are not present in the Temp
            //                          but that exists in the project and in the backup are copied back.
            //   b) With editor opened: The files were already copied so is something is updated, the changes are reflected.

            // Update: Starting from 2021.2 output files are prohibited (or at least largely discouraged). As such, all of the above comment does not
            // apply anymore. When the IDE integration (that will use the internals of source generators roslyn API) will be able to use the in-memory or compiler
            // cached versions of these files, the need of output the generated files on disk may be not necessary anymore. However that make really hard
            // to iterate and debug, since we can't anymore rely on the feature we had that didn't emit the generated file
            // However this is true onluy for VS (maybe VSCode) and Rider.

            using (new Profiler.Auto("WriteFile"))
            {
                executionContext.CancellationToken.ThrowIfCancellationRequested();
                //Always delete all the previously generated files
                if (Helpers.CanWriteFiles)
                {
                    var outputFolder = Path.Combine(Helpers.GetOutputPath(), $"{executionContext.Compilation.AssemblyName}");
                    if(Directory.Exists(outputFolder))
                        Directory.Delete(outputFolder, true);
                    if(codeGenContext.batch.Count != 0)
                        Directory.CreateDirectory(outputFolder);
                }
                if (codeGenContext.batch.Count == 0)
                    return;

                foreach (var nameAndSource in codeGenContext.batch)
                {
                    executionContext.CancellationToken.ThrowIfCancellationRequested();
                    var sourceText = SourceText.From(nameAndSource.Code, System.Text.Encoding.UTF8);
                    //Normalize filename for hint purpose. Special characters are not supported anymore
                    //var hintName = uniqueName.Replace('/', '_').Replace('+', '-');
                    //TODO: compute a normalized hash of that name using a common stable hash algorithm
                    var uniqueName = string.IsNullOrEmpty(nameAndSource.Namespace)
                        ? nameAndSource.GeneratedClassName
                        : $"{nameAndSource.Namespace}_{nameAndSource.GeneratedClassName}";
                    var sourcePath = Path.Combine($"{executionContext.Compilation.AssemblyName}", uniqueName);
                    var hintName = Utilities.TypeHash.FNV1A64(sourcePath).ToString();
                    //With the new version of roslyn, is necessary to add to the generate file
                    //a first line with #line1 "sourcecodefullpath" so that when debugging the right
                    //file is used. IMPORTANT: the #line directive should be not in the file you save on
                    //disk to correct match the debugging line
                    executionContext.AddSource(hintName, sourceText.WithInitialLineDirective(sourcePath));
                    if (Helpers.CanWriteFiles)
                        File.WriteAllText(Path.Combine(Helpers.GetOutputPath(), sourcePath), sourceText.ToString());
                }
            }
        }
    }
}

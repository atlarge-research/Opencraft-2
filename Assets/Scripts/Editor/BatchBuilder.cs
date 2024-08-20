using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using UnityEngine.UIElements;
using System.IO;
using UnityEditor.Build.Reporting;
using UnityEngine.UIElements;
using System.Drawing.Printing;

public class BatchBuilder : EditorWindow
{

    string defaultConfigPath = "C:\\Users\\joach\\Desktop\\Opencraft-2-VR\\Assets\\Resources\\cmdArgs";
    string defaultTargetPath = "C:\\Users\\joach\\Desktop\\Opencraft-2-VR\\Assets\\Resources\\cmdArgs.json";
    List<string> paths;

    //Thank you to user nratcliff on unity discussions for this function:
    //https://discussions.unity.com/t/getting-the-current-buildoptions/224799/2
 

    void BuildOne(string fileName)
    {
        BuildPlayerOptions opts = new BuildPlayerOptions();
        string location = $"C:\\Users\\joach\\Desktop\\Opencraft-2-VR\\Builds\\out\\{fileName}.apk";
        opts.locationPathName = location;
        opts.scenes = new[] { "Assets/Scenes/MainScene.unity"};
        opts.target = BuildTarget.Android;
        opts.options = BuildOptions.None | BuildOptions.Development | BuildOptions.AllowDebugging;

        BuildReport report = BuildPipeline.BuildPlayer(opts);

        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log("Build succeeded: " + summary.totalSize + " bytes");
        }

        if (summary.result == BuildResult.Failed)
        {
            Debug.Log("Build failed");
        }
    }

    void buildBatch()
    {
        foreach (string path in paths)
        {
            File.Copy(path, defaultTargetPath, true);

            BuildOne("HMD-S-T-rd-03.json");
        }
    }
    
    void buildOneTest()
    {
        string fname = "C:\\Users\\joach\\Desktop\\Opencraft-2-VR\\Assets\\Resources\\cmdArgs\\HMD-S-T-rd-03.json";
        File.Copy(fname, defaultTargetPath, true);
        BuildOne("HMD-S-T-rd-03.json");
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object 
        VisualElement root = rootVisualElement;
        VisualElement pathFieldLabel = new Label("The batch builder will build:");

        root.Add(pathFieldLabel);
        paths = new List<string>(Directory.GetFiles(defaultConfigPath));
        VisualElement list = new ListView(paths);

        Button buildOneButton = new Button(buildOneTest);
        buildOneButton.Add(new Label("build one"));
        root.Add(buildOneButton);

        Button buildAllButton = new Button(buildBatch);
        buildAllButton.Add(new Label("build All"));
        root.Add(buildAllButton);
    }
}

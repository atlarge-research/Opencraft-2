using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Opencraft.Bootstrap;
using Opencraft.Deployment;
using Opencraft.Player.Emulated;
using Unity.NetCode;
using Unity.VisualScripting;
using UnityEngine;
using WebSocketSharp;
#if UNITY_EDITOR
using ParrelSync;
using UnityEditor;
#endif

namespace Opencraft
{
    public static class CmdArgsReader
    {
#if UNITY_EDITOR
        private static EditorCmdArgs editorArgs;
#endif
        // Returns the string array of cmd line arguments from environment, ParrelSync, or an editor GameObject
        private static string[] GetCommandlineArgs()
        {
            string[] args = new[] { "" };
#if UNITY_EDITOR
            // ParrelSync clones can have arguments passed to them in the Clones Manager window
            editorArgs = (EditorCmdArgs)GameObject.FindFirstObjectByType(typeof(EditorCmdArgs));
            if (ClonesManager.IsClone())
            {
                // Get the custom arguments for this clone project.  
                args = ClonesManager.GetArgument().Split(' ');
            }
            else
            {
                // Otherwise, use arguments in editor MonoBehaviour 
                args = editorArgs.editorArgs.Split(' ');
            }
#else
            // Read from normal command line application arguments
            args = Environment.GetCommandLineArgs();
#endif
            return args;
        }
        
        public static bool ParseCmdArgs()
        {
            var arguments = GetCommandlineArgs();
            Debug.Log($"Parsing args: {String.Join(", ", arguments)}");
            if (!CommandLineParser.TryParse(arguments))
            {
                Debug.LogError("Parsing command line arguments failed!");
                return false;
            }

            bool isLocalConf = CommandLineParser.LocalConfigJson.Value != null;
            JsonCmdArgs localArgs = new JsonCmdArgs();
            if (isLocalConf)
            {
                Debug.Log("Using local config from file");
                localArgs = (JsonCmdArgs)CommandLineParser.LocalConfigJson.Value;
            }

            // ================== DEPLOYMENT ==================
            // Deployment configuration file, used to construct the Deployment Graph
            if (CommandLineParser.ImportDeploymentConfig.Value != null)
            {
                Config.DeploymentConfig = (JsonDeploymentConfig)CommandLineParser.ImportDeploymentConfig.Value;
                Config.isDeploymentService = true;
            }
            else
            {
                Config.isDeploymentService = false;
            }

            // Deployment ID
            if (CommandLineParser.DeploymentID.Value != null)
                Config.DeploymentID = (int)CommandLineParser.DeploymentID.Value;
            else
            {
                Config.DeploymentID = localArgs.DeploymentID;
            }

            // Get remote config flag
            #if UNITY_SERVER
            Config.GetRemoteConfig = false;
            #else
            if (CommandLineParser.GetRemoteConfig.Value != null)
                Config.GetRemoteConfig = (bool)CommandLineParser.GetRemoteConfig.Value;
            else
                Config.GetRemoteConfig = localArgs.GetRemoteConfig;
            #endif

            // Deployment service URL
            if (CommandLineParser.DeploymentURL.Value != null)
                Config.DeploymentURL = CommandLineParser.DeploymentURL.Value;
            else
                Config.DeploymentURL = localArgs.DeploymentURL;

            // Deployment port
            if (CommandLineParser.DeploymentPort.Value != null)
                Config.DeploymentPort = (ushort)CommandLineParser.DeploymentPort.Value;
            else
                Config.DeploymentPort = (ushort)localArgs.DeploymentPort;

            // ================== SIGNALING ==================
            // Signaling URL
            if (CommandLineParser.SignalingUrl.Value != null)
                Config.SignalingUrl = CommandLineParser.SignalingUrl.Value;
            else
                Config.SignalingUrl = localArgs.SignalingUrl;
            /* Signaling port
            if (CommandLineParser.SignalingPort.Value != null)
                Config.SignalingPort = (ushort)CommandLineParser.SignalingPort.Value;
            else
                Config.SignalingPort = (ushort)localArgs.SignalingPort;*/


            // ================== APPLICATION ==================
            // Debug
            Config.DebugEnabled = CommandLineParser.DebugEnabled.Defined || localArgs.DebugEnabled;
            // Seed
            MD5 md5Hasher = MD5.Create();
            if (CommandLineParser.Seed.Value != null)
            {
                var hashed = md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(CommandLineParser.Seed.Value));
                var ivalue = BitConverter.ToInt32(hashed, 0);
                Config.Seed = ivalue;
            }
            else
            {
                var hashed = md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(localArgs.Seed));
                var ivalue = BitConverter.ToInt32(hashed, 0);
                Config.Seed = ivalue;
            }

            // PlayType
            #if UNITY_SERVER
            Config.playTypes = GameBootstrap.BootstrapPlayTypes.Server;
            #else
            if (CommandLineParser.PlayType.Value != null)
                Config.playTypes = (GameBootstrap.BootstrapPlayTypes)CommandLineParser.PlayType.Value;
            else
                Config.playTypes = localArgs.PlayType;
            #endif
            
            // Server url
            if (CommandLineParser.ServerUrl.Value != null)
                Config.ServerUrl = (string)CommandLineParser.ServerUrl.Value;
            else
                Config.ServerUrl = (string)localArgs.ServerUrl;
            // Server port
            if (CommandLineParser.ServerPort.Value != null)
                Config.ServerPort = (ushort)CommandLineParser.ServerPort.Value;
            else
                Config.ServerPort = (ushort)localArgs.ServerPort;

            // Tick rates
            if (CommandLineParser.NetworkTickRate.Value != null)
                Config.NetworkTickRate = (int)CommandLineParser.NetworkTickRate.Value;
            else
                Config.NetworkTickRate = localArgs.NetworkTickRate;
            if (CommandLineParser.SimulationTickRate.Value != null)
                Config.SimulationTickRate = (int)CommandLineParser.SimulationTickRate.Value;
            else
                Config.SimulationTickRate = localArgs.SimulationTickRate;
            
            // Get take screenshots flag
            if (CommandLineParser.TakeScreenshots.Value != null)
                Config.TakeScreenshots = (bool)CommandLineParser.TakeScreenshots.Value;
            else
                Config.TakeScreenshots = localArgs.TakeScreenshots;
            
            // Get take screenshots interval
            if (CommandLineParser.TakeScreenshotsInterval.Value != null)
                Config.TakeScreenshotsInterval = (int)CommandLineParser.TakeScreenshotsInterval.Value;
            else
                Config.TakeScreenshotsInterval = localArgs.TakeScreenshotsInterval;
            
            // Get take screenshots save location
            if (CommandLineParser.ScreenshotFolder.Value != null)
                Config.ScreenshotFolder = (string)CommandLineParser.ScreenshotFolder.Value;
            else
                Config.ScreenshotFolder = localArgs.ScreenshotFolder;

            
            // Duration
            if (CommandLineParser.Duration.Value != null)
                Config.Duration = (int)CommandLineParser.Duration.Value;
            else
                Config.Duration = localArgs.Duration;

            // ================== MULTIPLAY ==================
            // Multiplay role
            #if UNITY_SERVER
            Config.multiplayStreamingRoles = MultiplayStreamingRoles.Disabled;
            #else
            if (CommandLineParser.MultiplayStreamingRole.Value != null)
                Config.multiplayStreamingRoles =
                    (MultiplayStreamingRoles)CommandLineParser.MultiplayStreamingRole.Value;
            else
                Config.multiplayStreamingRoles = localArgs.MultiplayStreamingRole;
            #endif
            
            if (CommandLineParser.SwitchToStreamDuration.Value != null)
                Config.SwitchToStreamDuration =
                    (int)CommandLineParser.SwitchToStreamDuration.Value;
            else
                Config.SwitchToStreamDuration = localArgs.SwitchToStreamDuration;

            // ================== EMULATION ==================
            // Emulation type
            if (CommandLineParser.EmulationType.Value != null)
                Config.EmulationType = (EmulationBehaviours)CommandLineParser.EmulationType.Value;
            else
                Config.EmulationType = localArgs.EmulationType;
            // Emulation file path
            if (CommandLineParser.EmulationFile.Value != null)
                Config.EmulationFilePath = CommandLineParser.EmulationFile.Value;
            else
                Config.EmulationFilePath = localArgs.EmulationFile;

            // Number of thin clients
            if (CommandLineParser.NumThinClientPlayers.Value != null)
                Config.NumThinClientPlayers = (int)CommandLineParser.NumThinClientPlayers.Value;
            else
                Config.NumThinClientPlayers = localArgs.NumThinClientPlayers;
            
            // Log statistics to csv file
            if (CommandLineParser.LogStats.Value != null)
                Config.LogStats = true;
            else
                Config.LogStats = localArgs.LogStats;
            
            // Statistics csv filepath
            if (CommandLineParser.StatsFilePath.Value != null)
                Config.StatsFilePath = CommandLineParser.StatsFilePath.Value;
            else
                Config.StatsFilePath = localArgs.StatsFile;

#if UNITY_EDITOR

            Debug.Log("Overriding config with editor vars.");
            // Override PlayType, NumThinClients, ServerAddress, and ServerPort from editor settings 
            string s_PrefsKeyPrefix = $"MultiplayerPlayMode_{Application.productName}_";
            string s_PlayModeTypeKey = s_PrefsKeyPrefix + "PlayMode_Type";
            string s_RequestedNumThinClientsKey = s_PrefsKeyPrefix + "NumThinClients";
            string s_AutoConnectionAddressKey = s_PrefsKeyPrefix + "AutoConnection_Address";
            string s_AutoConnectionPortKey = s_PrefsKeyPrefix + "AutoConnection_Port";
            // Editor PlayType
            ClientServerBootstrap.PlayType editorPlayType =
                (ClientServerBootstrap.PlayType)EditorPrefs.GetInt(s_PlayModeTypeKey,
                    (int)ClientServerBootstrap.PlayType.ClientAndServer);
            if (Config.playTypes != GameBootstrap.BootstrapPlayTypes.StreamedClient)
                Config.playTypes = (GameBootstrap.BootstrapPlayTypes)editorPlayType;
            // Number thin clients
            int editorNumThinClients = EditorPrefs.GetInt(s_RequestedNumThinClientsKey, 0);
            Config.NumThinClientPlayers = editorNumThinClients;
            // Server address
            string editorServerAddress = EditorPrefs.GetString(s_AutoConnectionAddressKey, "127.0.0.1");
            Config.ServerUrl = editorServerAddress;
            //Server port
            int editorServerPort = EditorPrefs.GetInt(s_AutoConnectionPortKey, 7979);
            if (editorServerPort != 0)
                Config.ServerPort = (ushort)editorServerPort;

            // Override Deployment Config using this MonoBehaviour's attributes
            if (editorArgs.useDeploymentConfig && !ClonesManager.IsClone())
            {
                if (editorArgs.deploymentConfig.IsNullOrEmpty())
                {
                    Debug.Log($"UseDeploymentConfig flag set but deploymentConfig is empty");
                }
                else
                {
                    //Use Newtonsoft JSON parsing to support enum serialization to/from string
                    Config.DeploymentConfig = JsonConvert.DeserializeObject<JsonDeploymentConfig>(editorArgs.deploymentConfig);
                    if (Config.DeploymentConfig.IsUnityNull())
                    {
                        Debug.Log($"Json Could not parse deploymentConfig!");
                    }
                    else
                    {
                        Config.isDeploymentService = true;
                    }
                }
            }

#endif

            // Sanity checks
            if (Config.GetRemoteConfig && Config.DeploymentURL.IsNullOrEmpty())
            {
                Debug.Log($"Remote config flag set with no deployment service url provided, using loopback!");
                Config.DeploymentURL = "127.0.0.1";
            }

            if (Config.GetRemoteConfig && Config.DeploymentID == -1)
            {
                Debug.Log($"Remote config flag set with no deployment ID provided, using 0!");
                Config.DeploymentID = 0;
            }

            if (Config.playTypes == GameBootstrap.BootstrapPlayTypes.Server &&
                Config.multiplayStreamingRoles != MultiplayStreamingRoles.Disabled)
            {
                Debug.Log("Cannot run Multiplay streaming on Server, disabling Multiplay!");
                Config.multiplayStreamingRoles = MultiplayStreamingRoles.Disabled;
            }

            if (Config.playTypes != GameBootstrap.BootstrapPlayTypes.Server && Config.ServerUrl.IsNullOrEmpty())
            {
                Debug.Log($"No server ip given to client! Falling back to 127.0.0.1 ");
                Config.ServerUrl = $"127.0.0.1";
            }

            if (Config.multiplayStreamingRoles != MultiplayStreamingRoles.Disabled &&
                Config.SignalingUrl.IsNullOrEmpty())
            {
                Debug.Log("Run as Multiplay streaming host or client with no signaling server!");
            }

            return true;
        }

    }
}
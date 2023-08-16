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
    /// <summary>
    /// Reads configuration from application environment (args or config file), ParrelSync, or an editor GameObject.
    /// </summary>
    public class CmdArgsReader : MonoBehaviour
    {
        public string editorArgs;
        public bool useDeploymentConfig = false;
        [Multiline(10)]public string deploymentConfig;
        
        // Returns the string array of cmd line arguments from environment, ParrelSync, or an editor GameObject
        private string[] GetCommandlineArgs()
        {
            string[] args = new[] { "" };
#if UNITY_EDITOR
            // ParrelSync clones can have arguments passed to them in the Clones Manager window
            if (ClonesManager.IsClone())
            {
                // Get the custom arguments for this clone project.  
                args = ClonesManager.GetArgument().Split(' ');
            } else {
                // Otherwise, use arguments in this MonoBehaviour 
                args = editorArgs.Split(' ');
            }
#else
            args = Environment.GetCommandLineArgs();
#endif
            return args;
        }

        public bool ParseCmdArgs()
        {
            var arguments = GetCommandlineArgs();
            Debug.Log($"Parsing args: {String.Join(" ", arguments)}");
            if (!CommandLineParser.TryParse(arguments))
            {
                Debug.LogError("Parsing command line arguments failed!");
                return false;
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
                Config.DeploymentID = -1;
            
            // Get remote config flag
            if (CommandLineParser.GetRemoteConfig.Value != null)
                Config.GetRemoteConfig = (bool)CommandLineParser.GetRemoteConfig.Value;

            // Deployment service URL
            if (CommandLineParser.DeploymentURL.Value != null)
                Config.DeploymentURL = CommandLineParser.DeploymentURL.Value;
            
            // Deployment port
            if (CommandLineParser.DeploymentPort.Value != null)
                Config.DeploymentPort = (ushort)CommandLineParser.DeploymentPort.Value;
            else
                Config.DeploymentPort = 7980;
            
            // ================== SIGNALING ==================
            // Signaling URL
            if (CommandLineParser.SignalingUrl.Value != null)
                Config.SignalingUrl = CommandLineParser.SignalingUrl.Value;
            if (CommandLineParser.SignalingPort.Value != null)
                Config.SignalingPort = (ushort)CommandLineParser.SignalingPort.Value;
            else
                Config.SignalingPort = 7981;


            // ================== APPLICATION ==================
            // Debug
            Config.DebugEnabled = CommandLineParser.DebugEnabled.Defined;
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
                var hashed = md5Hasher.ComputeHash(Encoding.UTF8.GetBytes("42"));
                var ivalue = BitConverter.ToInt32(hashed, 0);
                Config.Seed = ivalue;
            }
            // PlayType
            if (CommandLineParser.PlayType.Value != null)
                Config.playTypes = (GameBootstrap.BootstrapPlayTypes)CommandLineParser.PlayType.Value;
            else
                Config.playTypes = GameBootstrap.BootstrapPlayTypes.Client;
            // Server url
            if (CommandLineParser.ServerUrl.Value != null)
                Config.ServerUrl = CommandLineParser.ServerUrl.Value;
            else
                Config.ServerUrl = "127.0.0.1";
            // Server port
            if (CommandLineParser.ServerPort.Value != null)
                Config.ServerPort = (ushort)CommandLineParser.ServerPort.Value;
            else
                Config.ServerPort = 7979;
            
            // Tick rates
            if (CommandLineParser.NetworkTickRate.Value != null)
                Config.NetworkTickRate = (int)CommandLineParser.NetworkTickRate.Value;
            else
                Config.NetworkTickRate = 60;
            if (CommandLineParser.SimulationTickRate.Value != null)
                Config.SimulationTickRate = (int)CommandLineParser.SimulationTickRate.Value;
            else
                Config.SimulationTickRate = 60;
            
            
            
            // ================== MULTIPLAY ==================
            // Multiplay role
            if (CommandLineParser.MultiplayStreamingRole.Value != null)
                Config.multiplayStreamingRoles = (MultiplayStreamingRoles)CommandLineParser.MultiplayStreamingRole.Value;
            else
                Config.multiplayStreamingRoles = MultiplayStreamingRoles.Disabled;
            
            // ================== EMULATION ==================
            // Emulation type
            if (CommandLineParser.EmulationType.Value != null)
                Config.EmulationType = (EmulationBehaviours)CommandLineParser.EmulationType.Value;
            else
                Config.EmulationType = EmulationBehaviours.None;
            // Emulation file path
            if (CommandLineParser.EmulationFile.Value != null)
                Config.EmulationFilePath = CommandLineParser.EmulationFile.Value;
            else
                Config.EmulationFilePath = Application.persistentDataPath + '\\' + "recordedInputs.inputtrace";
            
            // Number of thin clients
            if (CommandLineParser.NumThinClientPlayers.Value != null)
                Config.NumThinClientPlayers = (int)CommandLineParser.NumThinClientPlayers.Value;
            else
                Config.NumThinClientPlayers = 0;
            
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
            Config.ServerPort = (ushort)editorServerPort;

            // Override Deployment Config using this MonoBehaviour's attributes
            if (useDeploymentConfig && !ClonesManager.IsClone())
            {
                if (deploymentConfig.IsNullOrEmpty())
                {
                    Debug.LogWarning($"UseDeploymentConfig flag set but deploymentConfig is empty");
                }
                else
                {
                    //Use Newtonsoft JSON parsing to support enum serialization to/from string
                    Config.DeploymentConfig = JsonConvert.DeserializeObject<JsonDeploymentConfig>(deploymentConfig);
                    if (Config.DeploymentConfig.IsUnityNull())
                    {
                        Debug.LogWarning($"Json Could not parse deploymentConfig!");
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
                Debug.LogWarning($"Remote config flag set with no deployment service url provided, using loopback!");
                Config.DeploymentURL = "127.0.0.1";
            }
            if (Config.GetRemoteConfig && Config.DeploymentID == -1)
            {
                Debug.LogWarning($"Remote config flag set with no deployment ID provided, using 0!");
                Config.DeploymentID = 0;
            }
            if (Config.playTypes == GameBootstrap.BootstrapPlayTypes.Server && Config.multiplayStreamingRoles != MultiplayStreamingRoles.Disabled)
            {
                Debug.LogWarning("Cannot run Multiplay streaming on Server, disabling Multiplay!");
                Config.multiplayStreamingRoles = MultiplayStreamingRoles.Disabled;
            }
            if (Config.playTypes != GameBootstrap.BootstrapPlayTypes.Server && Config.ServerUrl.IsNullOrEmpty() )
            {
                Debug.LogWarning($"No server ip given to client! Attempting to connect to loopback on {Config.ServerPort}");
                Config.ServerUrl = $"127.0.0.1:{Config.ServerPort}";
            }

            if (Config.multiplayStreamingRoles != MultiplayStreamingRoles.Disabled && Config.SignalingUrl.IsNullOrEmpty())
            {
                Debug.LogWarning("Run as Multiplay streaming host or client with no signaling server!");
            }
            
            return true;
        }
        
    }
}
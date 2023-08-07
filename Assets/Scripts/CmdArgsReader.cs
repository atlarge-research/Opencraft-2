using System;
using System.Security.Cryptography;
using System.Text;
using Opencraft.Bootstrap;
using Opencraft.Player.Emulated;
using Opencraft.Player.Multiplay;
using Unity.NetCode;
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

            // ================== SIGNALING ==================
            // Signaling URL
            if (CommandLineParser.SignalingUrl.Value != null)
                Config.SignalingUrl = CommandLineParser.SignalingUrl.Value;
            // Fetch config from signaling server
            Config.ConfigFromSignaling = CommandLineParser.ConfigFromSignaling.Defined;
       
            
            // ================== APPLICATION ==================
            // Debug
            Config.DebugEnabled = CommandLineParser.DebugEnabled.Defined;
            // Seed
            if (CommandLineParser.Seed.Value != null)
            {
                MD5 md5Hasher = MD5.Create();
                var hashed = md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(CommandLineParser.Seed.Value));
                var ivalue = BitConverter.ToInt32(hashed, 0);
                Config.Seed = ivalue;
            }
            else
                Config.Seed = 42;
            // PlayType
            if (CommandLineParser.PlayType.Value != null)
                Config.PlayType = (GameBootstrap.BootstrapPlayType)CommandLineParser.PlayType.Value;
            else
                Config.PlayType = GameBootstrap.BootstrapPlayType.ClientAndServer;
            // Server url
            if (CommandLineParser.ServerUrl.Value != null)
                Config.ServerUrl = CommandLineParser.ServerUrl.Value;
            else
                Config.ServerUrl = "127.0.01";
            // Server port
            if (CommandLineParser.ServerPort.Value != null)
                Config.ServerPort = (int)CommandLineParser.ServerPort.Value;
            else
                Config.ServerPort = 7979;
            
            // ================== MULTIPLAY ==================
            // Multiplay role
            if (CommandLineParser.MultiplayStreamingRole.Value != null)
                Config.MultiplayStreamingRole = (MultiplayStreamingRole)CommandLineParser.MultiplayStreamingRole.Value;
            else
                Config.MultiplayStreamingRole = MultiplayStreamingRole.Disabled;
            
            // ================== EMULATION ==================
            // Emulation type
            if (CommandLineParser.EmulationType.Value != null)
                Config.EmulationType = (EmulationType)CommandLineParser.EmulationType.Value;
            else
                Config.EmulationType = EmulationType.None;
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
            ClientServerBootstrap.PlayType editorPlayType = (ClientServerBootstrap.PlayType) EditorPrefs.GetInt(s_PlayModeTypeKey, (int) ClientServerBootstrap.PlayType.ClientAndServer);
            if (Config.PlayType != GameBootstrap.BootstrapPlayType.ThinClient)
                Config.PlayType = (GameBootstrap.BootstrapPlayType)editorPlayType;
            // Number thin clients
            int editorNumThinClients = EditorPrefs.GetInt(s_RequestedNumThinClientsKey, 0);
            if (Config.PlayType == GameBootstrap.BootstrapPlayType.ThinClient && Config.NumThinClientPlayers == 0)
                Config.NumThinClientPlayers = editorNumThinClients;
            // Server address
            string editorServerAddress = EditorPrefs.GetString(s_AutoConnectionAddressKey, "127.0.0.1");
            Config.ServerUrl = editorServerAddress;
            //Server port
            int editorServerPort = EditorPrefs.GetInt(s_AutoConnectionPortKey, 7979);
            Config.ServerPort = editorServerPort;
#endif
            
            // Sanity checks
            if (Config.PlayType == GameBootstrap.BootstrapPlayType.ThinClient && Config.NumThinClientPlayers == 0)
            {
                Debug.LogError("Run as thin client but with zero players, exiting!");
                return false;
            }
            if (Config.PlayType == GameBootstrap.BootstrapPlayType.Server && Config.MultiplayStreamingRole != MultiplayStreamingRole.Disabled)
            {
                Debug.LogWarning("Cannot run Multiplay streaming on Server, disabling Multiplay!");
                Config.MultiplayStreamingRole = MultiplayStreamingRole.Disabled;
            }
            if (Config.PlayType != GameBootstrap.BootstrapPlayType.Server && Config.ServerUrl.IsNullOrEmpty() )
            {
                Debug.LogWarning($"No server ip given to client! Attempting to connect to loopback on {Config.ServerPort}");
                Config.ServerUrl = $"127.0.0.1:{Config.ServerPort}";
            }

            if (Config.MultiplayStreamingRole != MultiplayStreamingRole.Disabled && Config.SignalingUrl.IsNullOrEmpty())
            {
                Debug.LogError("Run as Multiplay streaming host or client with no signaling server, exiting!");
                return false;
            }
            
            
            
            return true;
        }

    }
}
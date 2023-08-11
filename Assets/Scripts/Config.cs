using System.IO;
using Opencraft.Bootstrap;
using Opencraft.Player.Emulated;
using Opencraft.Player.Multiplay;
using Unity.Logging;
using Unity.NetCode;
using UnityEditor;
using UnityEngine;

namespace Opencraft
{
    /// <summary>
    /// Static global class holding configuration parameters. Filled by <see cref="CmdArgsReader"/>
    /// </summary>
    public static class Config
    {
        // ================== DEPLOYMENT ==================
        public static JsonDeploymentConfig DeploymentConfig;
        public static int DeploymentID;
        public static bool GetRemoteConfig;
        public static bool isDeploymentService;
        public static string DeploymentURL;
        public static ushort DeploymentPort;

        // ================== SIGNALING ==================
        public static string SignalingUrl;

        // ================== APPLICATION ==================
        public static bool DebugEnabled;
        public static int Seed;
        public static GameBootstrap.BootstrapPlayType PlayType;
        public static string ServerUrl;
        public static ushort ServerPort;

        // ================== MULTIPLAY ==================
        public static MultiplayStreamingRole MultiplayStreamingRole;
        
        // ================== EMULATION ==================
        public static EmulationType EmulationType;
        public static string EmulationFilePath;
        public static int NumThinClientPlayers;
        
    }
}
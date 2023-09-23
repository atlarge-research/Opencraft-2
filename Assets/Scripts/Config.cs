using Opencraft.Bootstrap;
using Opencraft.Deployment;
using Opencraft.Player.Emulated;


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
        //public static ushort SignalingPort;

        // ================== APPLICATION ==================
        public static bool DebugEnabled;
        public static int Seed;
        public static GameBootstrap.BootstrapPlayTypes playTypes;
        public static string ServerUrl;
        public static ushort ServerPort;
        public static int NetworkTickRate;
        public static int SimulationTickRate;
        public static bool TakeScreenshots;
        public static string ScreenshotFolder;
        public static int TakeScreenshotsInterval;
        public static int Duration;

        // ================== MULTIPLAY ==================
        public static MultiplayStreamingRoles multiplayStreamingRoles;
        public static int SwitchToStreamDuration;
        
        // ================== EMULATION ==================
        public static EmulationBehaviours EmulationType;
        public static string EmulationFilePath;
        public static int NumThinClientPlayers;
        
        // ================== STATISTICS ==================
        public static bool LogStats;
        public static string StatsFilePath;
    }
}
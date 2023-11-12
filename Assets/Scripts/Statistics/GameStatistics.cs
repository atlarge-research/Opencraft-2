using Unity.Profiling;
#if UNITY_EDITOR
using Unity.Profiling.Editor;
#endif

namespace Opencraft.Statistics
{
    /// <summary>
    ///  Profiler module for game-specific performance data
    /// </summary>
    public class GameStatistics
    {
        public static readonly ProfilerCategory GameStatisticsCategory = ProfilerCategory.Scripts;
        
        public const string NumTerrainAreasClientName = "Number of Terrain Areas (Client)";
        public static readonly ProfilerCounterValue<int> NumTerrainAreasClient =
            new ProfilerCounterValue<int>(GameStatisticsCategory, NumTerrainAreasClientName, ProfilerMarkerDataUnit.Count);
        public const string NumTerrainAreasServerName = "Number of Terrain Areas (Server)";
        public static readonly ProfilerCounterValue<int> NumTerrainAreasServer =
            new ProfilerCounterValue<int>(GameStatisticsCategory, NumTerrainAreasServerName, ProfilerMarkerDataUnit.Count);
        
    }
#if UNITY_EDITOR
    [System.Serializable]
    [ProfilerModuleMetadata("Game Statistics")] 
    public class GameProfilerModule : ProfilerModule
    {
        static readonly ProfilerCounterDescriptor[] k_Counters = new ProfilerCounterDescriptor[]
        {
            new ProfilerCounterDescriptor(GameStatistics.NumTerrainAreasClientName, GameStatistics.GameStatisticsCategory),
            new ProfilerCounterDescriptor(GameStatistics.NumTerrainAreasServerName, GameStatistics.GameStatisticsCategory)
        };

        // Ensure that both ProfilerCategory.Scripts and ProfilerCategory.Memory categories are enabled when our module is active.
        static readonly string[] k_AutoEnabledCategoryNames = new string[]
        {
            ProfilerCategory.Scripts.Name
        };


        // Pass the auto-enabled category names to the base constructor.
        public GameProfilerModule() : base(k_Counters, autoEnabledCategoryNames: k_AutoEnabledCategoryNames) { }
    }
#endif
    
    /// <summary>
    ///  Profiler module for Netcode For Entities performance data
    /// </summary>
    public class NetCodeStatistics
    {
        public static readonly ProfilerCategory NetCodeStatisticsCategory = ProfilerCategory.Network;
        
        public const string SnapshotSizeInBitsName = "NFE Snapshot Size (bits)";
        public static readonly ProfilerCounterValue<uint> SnapshotSizeInBits =
            new ProfilerCounterValue<uint>(NetCodeStatisticsCategory, SnapshotSizeInBitsName, ProfilerMarkerDataUnit.Count);
        
        public const string SnapshotTickName = "NFE Snapshot Tick";
        public static readonly ProfilerCounterValue<uint> SnapshotTick =
            new ProfilerCounterValue<uint>(NetCodeStatisticsCategory, SnapshotTickName , ProfilerMarkerDataUnit.Count);
        
        public const string RTTName = "NFE RTT";
        public static readonly ProfilerCounterValue<float> RTT =
            new ProfilerCounterValue<float>(NetCodeStatisticsCategory, RTTName, ProfilerMarkerDataUnit.Count);
        
        public const string JitterName = "NFE Jitter";
        public static readonly ProfilerCounterValue<float> Jitter =
            new ProfilerCounterValue<float>(NetCodeStatisticsCategory, JitterName, ProfilerMarkerDataUnit.Count);
        
        public const string PredictionErrorsName = "NFE Prediction Errors";
        public static readonly ProfilerCounterValue<float> PredictionErrors =
            new ProfilerCounterValue<float>(NetCodeStatisticsCategory, PredictionErrorsName, ProfilerMarkerDataUnit.Count);
    }
#if UNITY_EDITOR
    [System.Serializable]
    [ProfilerModuleMetadata("NetCode Statistics")] 
    public class NetCodeProfilerModule : ProfilerModule
    {
        static readonly ProfilerCounterDescriptor[] k_Counters = new ProfilerCounterDescriptor[]
        {
            new ProfilerCounterDescriptor(NetCodeStatistics.SnapshotSizeInBitsName, NetCodeStatistics.NetCodeStatisticsCategory),
            new ProfilerCounterDescriptor(NetCodeStatistics.SnapshotTickName, NetCodeStatistics.NetCodeStatisticsCategory),
            new ProfilerCounterDescriptor(NetCodeStatistics.RTTName, NetCodeStatistics.NetCodeStatisticsCategory),
            new ProfilerCounterDescriptor(NetCodeStatistics.JitterName, NetCodeStatistics.NetCodeStatisticsCategory),
        };

        // Ensure that both ProfilerCategory.Scripts and ProfilerCategory.Memory categories are enabled when our module is active.
        static readonly string[] k_AutoEnabledCategoryNames = new string[]
        {
            ProfilerCategory.Network.Name
        };


        // Pass the auto-enabled category names to the base constructor.
        public NetCodeProfilerModule() : base(k_Counters, autoEnabledCategoryNames: k_AutoEnabledCategoryNames) { }
    }
#endif
}
using Unity.Profiling;
using Unity.Profiling.Editor;

namespace Opencraft.Statistics
{
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
}
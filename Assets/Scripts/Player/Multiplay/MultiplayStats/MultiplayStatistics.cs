using Unity.Profiling;

#if UNITY_EDITOR
using Unity.Profiling.Editor;
#endif


// Defines profiler counters for collection and display by profiler module
namespace Opencraft.Player.Multiplay.MultiplayStats
{
    public class MultiplayStatistics
    {
        public static readonly ProfilerCategory MultiplayCategory = ProfilerCategory.Scripts;
        
        public const string RoundTripTimeName = "Round Trip Time (ms)";
        public static readonly ProfilerCounterValue<double> MultiplayRoundTripTime =
            new ProfilerCounterValue<double>(MultiplayCategory, RoundTripTimeName, ProfilerMarkerDataUnit.Count);
        
        public const string BitRateName = "BitRate";
        public static readonly ProfilerCounterValue<double> MultiplayBitRate =
            new ProfilerCounterValue<double>(MultiplayCategory ,BitRateName , ProfilerMarkerDataUnit.Count);
        
        public const string FPSName = "FPS";
        public static readonly ProfilerCounterValue<double> MultiplayFPS =
            new ProfilerCounterValue<double>(MultiplayCategory ,"FPS" , ProfilerMarkerDataUnit.Count);
        
        public const string PacketLossName = "PacketLoss";
        public static readonly ProfilerCounterValue<double> MultiplayPacketLoss =
            new ProfilerCounterValue<double>(MultiplayCategory ,"PacketLoss" , ProfilerMarkerDataUnit.Percent);
    }
    
#if UNITY_EDITOR
    [System.Serializable]
    [ProfilerModuleMetadata("Multiplay Statistics")] 
    public class MultiplayProfilerModule : ProfilerModule
    {
        static readonly ProfilerCounterDescriptor[] k_Counters = new ProfilerCounterDescriptor[]
        {
            new ProfilerCounterDescriptor(MultiplayStatistics.RoundTripTimeName, MultiplayStatistics.MultiplayCategory),
            new ProfilerCounterDescriptor(MultiplayStatistics.BitRateName, MultiplayStatistics.MultiplayCategory),
            new ProfilerCounterDescriptor(MultiplayStatistics.FPSName, MultiplayStatistics.MultiplayCategory),
            new ProfilerCounterDescriptor(MultiplayStatistics.PacketLossName, MultiplayStatistics.MultiplayCategory),
        };

        // Ensure that both ProfilerCategory.Scripts and ProfilerCategory.Memory categories are enabled when our module is active.
        static readonly string[] k_AutoEnabledCategoryNames = new string[]
        {
            ProfilerCategory.Scripts.Name
        };


        // Pass the auto-enabled category names to the base constructor.
        public MultiplayProfilerModule() : base(k_Counters, autoEnabledCategoryNames: k_AutoEnabledCategoryNames) { }
    }
#endif
}
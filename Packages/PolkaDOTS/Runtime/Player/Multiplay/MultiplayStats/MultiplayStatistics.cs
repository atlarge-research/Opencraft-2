using Unity.Profiling;

#if UNITY_EDITOR
using Unity.Profiling.Editor;
#endif

namespace PolkaDOTS.Multiplay.MultiplayStats
{
    /// <summary>
    /// Profiler module for Multiplay render streaming performance data
    /// </summary>
    public class MultiplayStatistics
    {
        public static readonly ProfilerCategory MultiplayCategory = ProfilerCategory.Scripts;
        
        public const string RoundTripTimeName = "Multiplay RTT (ms)";
        public static readonly ProfilerCounterValue<double> MultiplayRoundTripTime =
            new ProfilerCounterValue<double>(MultiplayCategory, RoundTripTimeName, ProfilerMarkerDataUnit.Count);
        
        public const string BitRateInName = "Multiplay BitRate In";
        public static readonly ProfilerCounterValue<double> MultiplayBitRateIn =
            new ProfilerCounterValue<double>(MultiplayCategory ,BitRateInName  , ProfilerMarkerDataUnit.Count);
        
        public const string BitRateOutName = "Multiplay BitRate Out";
        public static readonly ProfilerCounterValue<double> MultiplayBitRateOut =
            new ProfilerCounterValue<double>(MultiplayCategory ,BitRateOutName  , ProfilerMarkerDataUnit.Count);
        
        public const string FPSName = "Multiplay FPS";
        public static readonly ProfilerCounterValue<double> MultiplayFPS =
            new ProfilerCounterValue<double>(MultiplayCategory ,FPSName  , ProfilerMarkerDataUnit.Count);
        
        public const string PacketLossName = "Multiplay PacketLoss";
        public static readonly ProfilerCounterValue<double> MultiplayPacketLoss =
            new ProfilerCounterValue<double>(MultiplayCategory ,PacketLossName , ProfilerMarkerDataUnit.Percent);
    }
    
#if UNITY_EDITOR
    [System.Serializable]
    [ProfilerModuleMetadata("Multiplay Statistics")] 
    public class MultiplayProfilerModule : ProfilerModule
    {
        static readonly ProfilerCounterDescriptor[] k_Counters = new ProfilerCounterDescriptor[]
        {
            new ProfilerCounterDescriptor(MultiplayStatistics.RoundTripTimeName, MultiplayStatistics.MultiplayCategory),
            new ProfilerCounterDescriptor(MultiplayStatistics.BitRateInName, MultiplayStatistics.MultiplayCategory),
            new ProfilerCounterDescriptor(MultiplayStatistics.BitRateOutName, MultiplayStatistics.MultiplayCategory),
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
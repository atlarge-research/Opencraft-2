using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Profiling;
#if UNITY_EDITOR
using Unity.Profiling.Editor;
#endif

namespace PolkaDOTS.Statistics
{
    /// <summary>
    /// Collects performance metrics
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateAfter(typeof(NetworkReceiveSystemGroup))]
    public partial struct StatisticsSystem : ISystem
    {
        
        public void OnCreate(ref SystemState state)
        {
            
            // Create network statistics monitoring singleton
            var typeList = new NativeArray<ComponentType>(4, Allocator.Temp);
            typeList[0] = ComponentType.ReadWrite<GhostMetricsMonitor>();
            typeList[1] = ComponentType.ReadWrite<NetworkMetrics>();
            typeList[2] = ComponentType.ReadWrite<SnapshotMetrics>();
            typeList[3] = ComponentType.ReadWrite<GhostMetrics>();
            //typeList[3] = ComponentType.ReadWrite<GhostNames>();
            //typeList[4] = ComponentType.ReadWrite<GhostMetrics>();
            //typeList[5] = ComponentType.ReadWrite<GhostSerializationMetrics>();
            //typeList[6] = ComponentType.ReadWrite<PredictionErrorNames>();
            //typeList[4] = ComponentType.ReadWrite<PredictionErrorMetrics>();

            var metricSingleton = state.EntityManager.CreateEntity(state.EntityManager.CreateArchetype(typeList));
            FixedString64Bytes singletonName = "NetCodeMetricsMonitor";
            state.EntityManager.SetName(metricSingleton, singletonName);
            
        }
        
        public void OnUpdate(ref SystemState state)
        {
            // Record terrain area data
            //var terrainCount = _terrainAreaQuery.CalculateEntityCount();
            //if (state.WorldUnmanaged.IsClient())
            //    GameStatistics.NumTerrainAreasClient.Value = terrainCount;
            //if(state.WorldUnmanaged.IsServer())
            //    GameStatistics.NumTerrainAreasServer.Value = terrainCount;
            
            // NetCode statistics
            if (SystemAPI.TryGetSingletonEntity<GhostMetricsMonitor>(out var entity))
            {
                if (state.EntityManager.HasComponent<NetworkMetrics>(entity))
                {
                    ref var networkMetrics = ref SystemAPI.GetSingletonRW<NetworkMetrics>().ValueRW;
                    NetCodeStatistics.RTT.Value = networkMetrics.Rtt;
                    NetCodeStatistics.Jitter.Value = networkMetrics.Jitter;
                    /*networkMetrics.SampleFraction = m_TimeSamples[0].sampleFraction;
                    networkMetrics.TimeScale = m_TimeSamples[0].timeScale;
                    networkMetrics.InterpolationOffset = m_TimeSamples[0].interpolationOffset;
                    networkMetrics.InterpolationScale = m_TimeSamples[0].interpolationScale;
                    networkMetrics.CommandAge = m_TimeSamples[0].commandAge;
                    networkMetrics.Rtt = m_TimeSamples[0].rtt;
                    networkMetrics.Jitter = m_TimeSamples[0].jitter;
                    networkMetrics.SnapshotAgeMin = m_TimeSamples[0].snapshotAgeMin;
                    networkMetrics.SnapshotAgeMax = m_TimeSamples[0].snapshotAgeMax;*/
                }
                if (state.EntityManager.HasComponent<SnapshotMetrics>(entity))
                {
                    var snapshotMetrics = SystemAPI.GetSingletonRW<SnapshotMetrics>().ValueRO;
                    NetCodeStatistics.SnapshotSizeInBits.Value = snapshotMetrics.TotalSizeInBits;
                    NetCodeStatistics.SnapshotTick.Value = snapshotMetrics.SnapshotTick;
                    /*snapshotMetrics.SnapshotTick = m_SnapshotTicks[0];
                    snapshotMetrics.TotalSizeInBits = totalSize;
                    snapshotMetrics.TotalGhostCount = totalCount;
                    snapshotMetrics.DestroyInstanceCount = hasSnapshotStats ? m_SnapshotStats[0] : 0;
                    snapshotMetrics.DestroySizeInBits = hasSnapshotStats ? m_SnapshotStats[1] : 0;*/
                }
            }
            
        }
    }
    
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
        
        /*public const string PredictionErrorsName = "NFE Prediction Errors";
        public static readonly ProfilerCounterValue<float> PredictionErrors =
            new ProfilerCounterValue<float>(NetCodeStatisticsCategory, PredictionErrorsName, ProfilerMarkerDataUnit.Count);*/
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
using Opencraft.Terrain.Authoring;
using PolkaDOTS;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.NetCode;
using Unity.NetCode.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;
#if UNITY_EDITOR
using Unity.Profiling.Editor;
#endif


namespace Opencraft.Statistics
{
    /// <summary>
    /// Extend PolkaDOTS with additional performance metric 
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateAfter(typeof(NetworkReceiveSystemGroup))]
    [UniqueSystem]
    public partial struct StatisticsSystem : ISystem
    {
        private EntityQuery _terrainAreaQuery;
        private EntityQuery _playerQuery;
        private bool first;
        
        public void OnCreate(ref SystemState state)
        {
            _terrainAreaQuery= new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TerrainArea>()
                .Build(state.EntityManager);
            _playerQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PolkaDOTS.Player, PlayerInGame>()
                .Build(state.EntityManager);
            first = true;
        }
        
        public void OnUpdate(ref SystemState state)
        {
            if (first)
            {
                if (ApplicationConfig.LogStats)
                {
                    Debug.Log("Adding terrain areas statistics recorders");
                    PolkaDOTS.Statistics.StatisticsWriter writer = PolkaDOTS.Statistics.StatisticsWriterInstance.instance;
                    writer.AddStatisticRecorder("Number of Terrain Areas (Client)", ProfilerCategory.Scripts);
                    writer.AddStatisticRecorder("Number of Terrain Areas (Server)", ProfilerCategory.Scripts); 
                    writer.AddStatisticRecorder("Number of Players (Client)", ProfilerCategory.Scripts);
                    writer.AddStatisticRecorder("Number of Players (Server)", ProfilerCategory.Scripts); 
                }
                first = false;
            }
            // Record terrain area data
            var terrainCount = _terrainAreaQuery.CalculateEntityCount();
            var playerCount = _playerQuery.CalculateEntityCount();
            if (state.WorldUnmanaged.IsClient())
            {
                GameStatistics.NumTerrainAreasClient.Value = terrainCount;
                GameStatistics.NumPlayersClient.Value = playerCount;
            }

            if (state.WorldUnmanaged.IsServer())
            {
                GameStatistics.NumTerrainAreasServer.Value = terrainCount;
                GameStatistics.NumPlayersServer.Value = playerCount;
            }



        }
    }
    
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
        public const string NumPlayersClientName = "Number of Players (Client)";
        public static readonly ProfilerCounterValue<int> NumPlayersClient =
            new ProfilerCounterValue<int>(GameStatisticsCategory, NumPlayersClientName, ProfilerMarkerDataUnit.Count);
        public const string NumPlayersServerName = "Number of Players (Server)";
        public static readonly ProfilerCounterValue<int> NumPlayersServer =
            new ProfilerCounterValue<int>(GameStatisticsCategory, NumPlayersServerName, ProfilerMarkerDataUnit.Count);
        
    }
#if UNITY_EDITOR
    [System.Serializable]
    [ProfilerModuleMetadata("Game Statistics")] 
    public class GameProfilerModule : ProfilerModule
    {
        static readonly ProfilerCounterDescriptor[] k_Counters = new ProfilerCounterDescriptor[]
        {
            new ProfilerCounterDescriptor(GameStatistics.NumTerrainAreasClientName, GameStatistics.GameStatisticsCategory),
            new ProfilerCounterDescriptor(GameStatistics.NumTerrainAreasServerName, GameStatistics.GameStatisticsCategory),
            new ProfilerCounterDescriptor(GameStatistics.NumPlayersClientName, GameStatistics.GameStatisticsCategory),
            new ProfilerCounterDescriptor(GameStatistics.NumPlayersServerName, GameStatistics.GameStatisticsCategory)
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
    
}
using Opencraft.Player.Authoring;
using Opencraft.Terrain.Authoring;
using PolkaDOTS;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
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
        public static ProfilerMarker _markerStatsSystem = new ProfilerMarker("StatisticsSystem");

        public void OnCreate(ref SystemState state)
        {
            _terrainAreaQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TerrainArea>()
                .Build(state.EntityManager);
            _playerQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PlayerComponent, PlayerInGame>()
                .Build(state.EntityManager);
            first = true;
        }

        public void OnUpdate(ref SystemState state)
        {
            _markerStatsSystem.Begin();
            if (first)
            {
                if (ApplicationConfig.LogStats)
                {
                    Debug.Log("Adding terrain areas statistics recorders");
                    PolkaDOTS.Statistics.StatisticsWriter writer = PolkaDOTS.Statistics.StatisticsWriterInstance.instance;
                    if (ApplicationConfig.ActiveLogic)
                    {
                        writer.AddStatisticRecorder(GameStatistics.NumGateBlocksName, ProfilerCategory.Scripts);
                        writer.AddStatisticRecorder(GameStatistics.NumInputTypeBlocksName, ProfilerCategory.Scripts);
                    }
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
            _markerStatsSystem.End();
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

        public const string NumGateBlocksName = "NumGateBlocks";
        public static readonly ProfilerCounterValue<int> NumGateTypeBlocks =
            new ProfilerCounterValue<int>(GameStatisticsCategory, NumGateBlocksName, ProfilerMarkerDataUnit.Count);

        public const string NumInputTypeBlocksName = "NumInputTypeBlocks";
        public static readonly ProfilerCounterValue<int> NumInputTypeBlocks =
            new ProfilerCounterValue<int>(GameStatisticsCategory, NumInputTypeBlocksName, ProfilerMarkerDataUnit.Count);

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
            new ProfilerCounterDescriptor(GameStatistics.NumPlayersServerName, GameStatistics.GameStatisticsCategory),
            new ProfilerCounterDescriptor(GameStatistics.NumGateBlocksName, GameStatistics.GameStatisticsCategory),
            new ProfilerCounterDescriptor(GameStatistics.NumInputTypeBlocksName, GameStatistics.GameStatisticsCategory),
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
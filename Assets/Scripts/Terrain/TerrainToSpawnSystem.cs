using System.Collections.Generic;
using Opencraft.Terrain;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Utilities;
using Opencraft.ThirdParty;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Physics;
using Unity.Profiling;

namespace Opencraft.Terrain
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TerrainGenerationSystem))]
    [BurstCompile]
    // System that determines what new terrain areas to spawn based on player location
    public partial struct TerrainToSpawn : ISystem
    {
        private EntityQuery _terrainAreasQuery;
        private NativeArray<TerrainArea> terrainAreas;
        private ProfilerMarker markerPlayerTerrainGenCheck;

        public void OnCreate(ref SystemState state)
        {
            // Wait for scene load/baking to occur before updates. 
            state.RequireForUpdate<TerrainSpawner>();
            state.RequireForUpdate<TerrainAreasToSpawn>();
            _terrainAreasQuery = SystemAPI.QueryBuilder().WithAll<TerrainArea>().Build();
            markerPlayerTerrainGenCheck = new ProfilerMarker("PlayerTerrainGenCheck");
        }

        public void OnUpdate(ref SystemState state)
        {


            // Fetch the terrain spawner entity and component
            var terrainSpawner = SystemAPI.GetSingleton<TerrainSpawner>();
            Entity terrainSpawnerEntity = SystemAPI.GetSingletonEntity<TerrainSpawner>();
            terrainAreas = _terrainAreasQuery.ToComponentDataArray<TerrainArea>(state.WorldUpdateAllocator);

            // Determine what chunks need to be spawned
            var toSpawnbuffer = SystemAPI.GetBuffer<TerrainAreasToSpawn>(terrainSpawnerEntity);
            DynamicBuffer<int3> chunksToSpawnBuffer = toSpawnbuffer.Reinterpret<int3>();
            markerPlayerTerrainGenCheck.Begin();
            // todo - make this parallel
            var areasPlayersCloseTo = new NativeHashSet<int3>(32, Allocator.TempJob);
            int blocksPerSide = terrainSpawner.blocksPerSide;
            int viewRange = terrainSpawner.terrainSpawnRange;
            foreach (var transform in SystemAPI.Query<LocalTransform>().WithAll<Player.Authoring.Player, Simulate>())
            {
                var pos = transform.Position;
                int3 playerChunk =new int3(
                    (int) (math.floor(pos.x / blocksPerSide )),
                    (int) (math.floor(pos.y / blocksPerSide )),
                    (int) (math.floor(pos.z / blocksPerSide )));
                var viewRangeSide = (viewRange +1+ viewRange);
                // Set of areas forming cube around players current area
                NativeHashSet<int3> nearbyAreas = new NativeHashSet<int3>(viewRangeSide * viewRangeSide * viewRangeSide, Allocator.Temp);
                for (int i = -viewRange; i < viewRange; i++)
                {
                    for (int j = -viewRange; j < viewRange; j++)
                    {
                        for (int k = -viewRange; k < viewRange; k++)
                        {
                            nearbyAreas.Add(playerChunk + new int3(i,j,k));
                        }
                    }
                }
            
                // O(n) in number of areas, may be further improvement by partitioning around players in advance
                foreach (var terrainArea in terrainAreas)
                {
                    if (nearbyAreas.Contains(terrainArea.location))
                    {
                        nearbyAreas.Remove(terrainArea.location);
                    }
                }
                // Copy the nearby areas of this player that aren't yet spawned to the global hashset
                areasPlayersCloseTo.UnionWith(nearbyAreas);
            }
            // Mark areas that need to be spawned
            chunksToSpawnBuffer.AddRange(areasPlayersCloseTo.ToNativeArray(Allocator.Temp));
            areasPlayersCloseTo.Dispose();
            markerPlayerTerrainGenCheck.End();
            
        }
    }
}
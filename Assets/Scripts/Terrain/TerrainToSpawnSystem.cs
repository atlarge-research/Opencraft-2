using Opencraft.Player.Authoring;
using Opencraft.Terrain.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;

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
            state.RequireForUpdate<TerrainColumnsToSpawn>();
            _terrainAreasQuery = SystemAPI.QueryBuilder().WithAll<TerrainArea>().Build();
            markerPlayerTerrainGenCheck = new ProfilerMarker("PlayerTerrainGenCheck");
        }

        public void OnUpdate(ref SystemState state)
        {


            // Fetch the terrain spawner entity and component
            //var terrainSpawner = SystemAPI.GetSingleton<TerrainSpawner>();
            Entity terrainSpawnerEntity = SystemAPI.GetSingletonEntity<TerrainSpawner>();
            terrainAreas = _terrainAreasQuery.ToComponentDataArray<TerrainArea>(state.WorldUpdateAllocator);

            // Determine what chunks need to be spawned
            var toSpawnbuffer = SystemAPI.GetBuffer<TerrainColumnsToSpawn>(terrainSpawnerEntity);
            DynamicBuffer<int2> chunksToSpawnBuffer = toSpawnbuffer.Reinterpret<int2>();
            markerPlayerTerrainGenCheck.Begin();
            // todo - make this parallel
            var areasPlayersCloseTo = new NativeHashSet<int2>(32, Allocator.TempJob);
            int viewRange = Env.getTerrainSpawnRange();
            foreach (var transform in SystemAPI.Query<LocalTransform>().WithAll<PlayerComponent, Simulate>())
            {
                var pos = transform.Position;
                int2 playerColumn =new int2(
                    (int) (math.floor(pos.x / Env.AREA_SIZE )),
                    (int) (math.floor(pos.z / Env.AREA_SIZE )));
                var viewRangeSide = (viewRange +1+ viewRange);
                // Set of areas forming cube around players current area
                NativeHashSet<int2> nearbyColumns = new NativeHashSet<int2>(viewRangeSide  * viewRangeSide, Allocator.Temp);
                for (int i = -viewRange; i < viewRange; i++)
                {
                    //for (int j = -viewRange; j < viewRange; j++)
                    //{
                    for (int k = -viewRange; k < viewRange; k++)
                    {
                        nearbyColumns.Add(playerColumn + new int2(i,k));
                    }
                    //}
                }
            
                // O(n) in number of areas, may be further improvement by partitioning around players in advance
                foreach (var terrainArea in terrainAreas)
                {
                    if (nearbyColumns.Contains(terrainArea.location.xz))
                    {
                        nearbyColumns.Remove(terrainArea.location.xz);
                    }
                }
                // Copy the nearby areas of this player that aren't yet spawned to the global hashset
                areasPlayersCloseTo.UnionWith(nearbyColumns);
            }
            // Mark areas that need to be spawned
            chunksToSpawnBuffer.AddRange(areasPlayersCloseTo.ToNativeArray(Allocator.Temp));
            areasPlayersCloseTo.Dispose();
            markerPlayerTerrainGenCheck.End();
            
        }
    }
}
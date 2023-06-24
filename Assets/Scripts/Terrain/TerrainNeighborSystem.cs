using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Opencraft.Terrain
{
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    // Calculates terrain neighbors and sets up links between them for easy access by terrain modification and meshing systems
    public partial class TerrainNeighborSystem : SystemBase
    {
        private EntityQuery _terrainChunkQuery;

        private EntityQuery _newSpawnQuery;
        private BufferLookup<TerrainBlocks> _terrainBufferLookup;

        protected override void OnCreate()
        {
            RequireForUpdate<TerrainArea>();
            RequireForUpdate<NewSpawn>();
            _terrainChunkQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TerrainArea>()
                .Build(EntityManager);
            _newSpawnQuery = SystemAPI.QueryBuilder().WithAll<NewSpawn>().Build();
        }


        [BurstCompile]
        protected override void OnUpdate()
        {
            if (_newSpawnQuery.IsEmpty)
                return;
            NativeArray<Entity> terrainChunkEntities = _terrainChunkQuery.ToEntityArray(Allocator.TempJob);
            NativeArray<TerrainArea> terrainAreas =
                _terrainChunkQuery.ToComponentDataArray<TerrainArea>(Allocator.TempJob);

            JobHandle handle = new SetChunkNeighborsJob()
            {
                terrainChunks = terrainAreas,
                terrainChunkEntities = terrainChunkEntities
            }.ScheduleParallel(Dependency);
            handle.Complete();

            terrainAreas.Dispose();
            terrainChunkEntities.Dispose();

        }
    }


    // When a new terrain area has been spawned, set it's neighbors, and update it's neighbors neighbors.
    [BurstCompile]
    [WithAll(typeof(NewSpawn))]
    public partial struct SetChunkNeighborsJob : IJobEntity
    {
        // thread safe as long as no terrain areas have the same location!
        [NativeDisableParallelForRestriction] public NativeArray<Entity> terrainChunkEntities;
        [NativeDisableParallelForRestriction] public NativeArray<TerrainArea> terrainChunks;

        public void Execute(Entity entity, ref TerrainArea terrainChunk)
        {
            for (int i = 0; i < terrainChunks.Length; i++)
            {
                var otherTerrainChunk = terrainChunks[i];
                int3 otherLoc = otherTerrainChunk.location;
                if (otherLoc.Equals(terrainChunk.location + new int3(1, 0, 0)))
                {
                    terrainChunk.neighborXP = terrainChunkEntities[i];
                    otherTerrainChunk.neighborXN = entity;
                }

                if (otherLoc.Equals(terrainChunk.location + new int3(-1, 0, 0)))
                {
                    terrainChunk.neighborXN = terrainChunkEntities[i];
                    otherTerrainChunk.neighborXP = entity;
                }

                if (otherLoc.Equals(terrainChunk.location + new int3(0, 1, 0)))
                {
                    terrainChunk.neighborYP = terrainChunkEntities[i];
                    otherTerrainChunk.neighborYN = entity;
                }

                if (otherLoc.Equals(terrainChunk.location + new int3(0, -1, 0)))
                {
                    terrainChunk.neighborYN = terrainChunkEntities[i];
                    otherTerrainChunk.neighborYP = entity;
                }

                if (otherLoc.Equals(terrainChunk.location + new int3(0, 0, 1)))
                {
                    terrainChunk.neighborZP = terrainChunkEntities[i];
                    otherTerrainChunk.neighborZN = entity;
                }

                if (otherLoc.Equals(terrainChunk.location + new int3(0, 0, -1)))
                {
                    terrainChunk.neighborZN = terrainChunkEntities[i];
                    otherTerrainChunk.neighborZP = entity;
                }
            }
        }
    }
}
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateAfter(typeof(TerrainGenerationSystem))]
public partial class RenderTerrainSystem : SystemBase
{
    //private EntityQuery query;
    private BufferLookup<TerrainBlocks> _bufferLookup;

    protected override void OnCreate()
    {
    }

    protected override void OnUpdate()
    {
        _bufferLookup = GetBufferLookup<TerrainBlocks>(true);
        var terrainAreasQuery = SystemAPI.QueryBuilder().WithAll<TerrainArea>().Build();
        NativeArray<Entity> terrainAreasEntities = terrainAreasQuery.ToEntityArray(WorldUpdateAllocator);
        NativeArray<TerrainArea> terrainAreas = terrainAreasQuery.ToComponentDataArray<TerrainArea>(WorldUpdateAllocator);
        var terrainSpawner = SystemAPI.GetSingleton<TerrainSpawner>();

        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        EntityCommandBuffer.ParallelWriter parallelEcb = ecb.AsParallelWriter();
        new CreateTerrainRenderPlanes
        {
            ecb = parallelEcb,
            face = terrainSpawner.TerrainFace,
            terrainAreasEntities = terrainAreasEntities,
            terrainAreas = terrainAreas,
            terrainBufferLookup = _bufferLookup,
            blocksPerChunkSide = terrainSpawner.blocksPerChunkSide

        }.ScheduleParallel();
        
        Dependency.Complete();
        ecb.Playback(EntityManager);
        ecb.Dispose();
        // Disable the NotRendered tag component from the areas we have rendered faces for
        var newSpawnQuery = SystemAPI.QueryBuilder().WithAll<NotRendered>().Build();
        EntityManager.SetComponentEnabled<NotRendered>(newSpawnQuery, false);

    }
}

[BurstCompile]
partial struct CreateTerrainRenderPlanes : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public Entity face;
    [ReadOnly] public NativeArray<Entity> terrainAreasEntities;
    [ReadOnly] public NativeArray<TerrainArea> terrainAreas;
    [ReadOnly] public BufferLookup<TerrainBlocks> terrainBufferLookup;
    public int blocksPerChunkSide;
    public void Execute(in DynamicBuffer<TerrainBlocks> terrainBlocksBuffer, in TerrainArea terrainArea, in NotRendered nr)
    {
        int3 area = terrainArea.location;
        bool neighborAreaUpExists = false;
        DynamicBuffer<TerrainBlocks> neighborAreaUp = new DynamicBuffer<TerrainBlocks>();
        bool neighborAreaDownExists = false;
        DynamicBuffer<TerrainBlocks> neighborAreaDown = new DynamicBuffer<TerrainBlocks>();
        bool neighborAreaLeftExists = false;
        DynamicBuffer<TerrainBlocks> neighborAreaLeft = new DynamicBuffer<TerrainBlocks>();
        bool neighborAreaRightExists = false;
        DynamicBuffer<TerrainBlocks> neighborAreaRight = new DynamicBuffer<TerrainBlocks>();
        bool neighborAreaForwardExists = false;
        DynamicBuffer<TerrainBlocks> neighborAreaForward = new DynamicBuffer<TerrainBlocks>();
        bool neighborAreaBackExists = false;
        DynamicBuffer<TerrainBlocks> neighborAreaBack = new DynamicBuffer<TerrainBlocks>();
        // find neighbor areas
        for (int i = 0; i < terrainAreas.Length; i++)
        {
            TerrainArea spawnedArea = terrainAreas[i];
            int3 loc = spawnedArea.location.xyz;
            Entity terrainAreaEntity = terrainAreasEntities[i];
            if (loc.Equals(area + new int3(blocksPerChunkSide,0,0)))
            {
                neighborAreaRightExists = true;
                neighborAreaRight = terrainBufferLookup[terrainAreaEntity];
            }
            else if (loc.Equals(area + new int3(-blocksPerChunkSide,0,0)))
            {
                neighborAreaLeftExists = true;
                neighborAreaLeft = terrainBufferLookup[terrainAreaEntity];
            }
            else if (loc.Equals(area + new int3(0,blocksPerChunkSide,0)))
            {
                neighborAreaUpExists = true;
                neighborAreaUp = terrainBufferLookup[terrainAreaEntity];
            }
            else if (loc.Equals(area + new int3(0,-blocksPerChunkSide,0)))
            {
                neighborAreaDownExists = true;
                neighborAreaDown = terrainBufferLookup[terrainAreaEntity];
            }
            else if (loc.Equals(area + new int3(0,0,blocksPerChunkSide)))
            {
                neighborAreaForwardExists = true;
                neighborAreaForward = terrainBufferLookup[terrainAreaEntity];
            }
            else if (loc.Equals(area + new int3(0,0,-blocksPerChunkSide)))
            {
                neighborAreaBackExists = true;
                neighborAreaBack = terrainBufferLookup[terrainAreaEntity];
            }
        }
        //Debug.Log($"TerrainArea {area} has: {neighborAreaRightExists},{neighborAreaLeftExists},{neighborAreaUpExists},{neighborAreaDownExists},{neighborAreaForwardExists},{neighborAreaBackExists}, ");
        
        // Calculate per-block visibility
        for (int i = 0; i < terrainBlocksBuffer.Length; i++)
        {
            int4 block = terrainBlocksBuffer[i].Value;
            // Check if this block is empty
            if (block.x == -1)
            {
                continue;
            }
            int3 blockNeighboringAreas = GetBlockNeighboringAreas(block.xyz);
            //Debug.Log($"Checking block {block} with neighboring areas {blockNeighboringAreas}");
            
            int4 currentNeighborBlock;
            // Check right
            if (blockNeighboringAreas.x == 1)
            {
                // Check value of block neighbor inside neighboring area
                currentNeighborBlock =  neighborAreaRightExists ? neighborAreaRight[i - blocksPerChunkSide + 1].Value : new int4(-1);
            }
            else
            {
                // Check value of block neighbor inside this area
                currentNeighborBlock = terrainBlocksBuffer[i + 1].Value;
            }
            if (currentNeighborBlock.w == -1) // Check block neighbor exists
            {
                SpawnFace(terrainArea.location + block.xyz, math.right());
            }
            // Check left
            if (blockNeighboringAreas.x == -1)
            {
                currentNeighborBlock =  neighborAreaLeftExists ? neighborAreaLeft[i + blocksPerChunkSide - 1].Value : new int4(-1);
            }
            else
            {
                currentNeighborBlock = terrainBlocksBuffer[i - 1].Value;
            }
            if (currentNeighborBlock.w == -1)
            {
                SpawnFace(terrainArea.location + block.xyz, math.left());
            }
            // Check up
            if (blockNeighboringAreas.y == 1)
            {
                currentNeighborBlock =  neighborAreaUpExists ? neighborAreaUp[i - (blocksPerChunkSide * (blocksPerChunkSide-1))].Value : new int4(-1);
            }
            else
            {
                currentNeighborBlock = terrainBlocksBuffer[i + blocksPerChunkSide].Value;
            }
            if (currentNeighborBlock.w == -1)
            {
                SpawnFace(terrainArea.location + block.xyz, math.up());
            }
            // Check down
            if (blockNeighboringAreas.y == -1)
            {
                currentNeighborBlock =  neighborAreaDownExists ? neighborAreaDown[i + (blocksPerChunkSide * (blocksPerChunkSide-1))].Value : new int4(-1);
            }
            else
            {
                currentNeighborBlock = terrainBlocksBuffer[i - blocksPerChunkSide].Value;
            }
            if (currentNeighborBlock.w == -1)
            {
                SpawnFace(terrainArea.location + block.xyz, math.down());
            }
            // Check forward
             if (blockNeighboringAreas.z == 1)
             {
                 currentNeighborBlock =  neighborAreaForwardExists ? neighborAreaForward[i - (blocksPerChunkSide * blocksPerChunkSide * (blocksPerChunkSide-1))].Value : new int4(-1);
             }
             else
             {
                 currentNeighborBlock = terrainBlocksBuffer[i + (blocksPerChunkSide * blocksPerChunkSide)].Value;
             }
             if (currentNeighborBlock.w == -1)
             {
                 SpawnFace(terrainArea.location + block.xyz, math.forward());
             }
            // Check back
            if (blockNeighboringAreas.z == -1)
            {
                currentNeighborBlock =  neighborAreaBackExists ? neighborAreaBack[i + (blocksPerChunkSide * blocksPerChunkSide * (blocksPerChunkSide-1))].Value : new int4(-1);
            }
            else
            {
                currentNeighborBlock = terrainBlocksBuffer[i - (blocksPerChunkSide * blocksPerChunkSide)].Value;
            }
            if (currentNeighborBlock.w == -1)
            {
                SpawnFace(terrainArea.location + block.xyz, math.back());
            }
        }
    }

    // Checks if a block is next to which neighboring chunks
    // (-1,0,1) for instance means the block is neighboring the chunk to the left and the chunk behind.
    private int3 GetBlockNeighboringAreas(int3 location)
    {
        // Check if neighboring at chunk - 1
        int neighborX = location.x == 0 ? -1 : 0;
        int neighborY = location.y == 0 ? -1 : 0;
        int neighborZ = location.z == 0 ? -1 : 0;
        // check if neighboring at chunk + 1
        neighborX = location.x == blocksPerChunkSide - 1 ? 1: neighborX;
        neighborY = location.y == blocksPerChunkSide - 1 ? 1: neighborY;
        neighborZ = location.z == blocksPerChunkSide - 1 ? 1: neighborZ;
        return new int3(neighborX,neighborY,neighborZ);
    }

    private void SpawnFace(int3 location, float3 direction)
    {
        Entity newFace = ecb.Instantiate(1, face);
        quaternion rotation = quaternion.LookRotationSafe( math.down(), direction);
        LocalTransform lt = new LocalTransform() { Position = location + 0.5f * direction + new float3(0.5f), Rotation = rotation, Scale = 1.0f};
        ecb.SetComponent(1, newFace, lt);
    }
}

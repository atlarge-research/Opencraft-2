using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[BurstCompile]
public partial struct TerrainGenerationSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // Wait for scene load/baking to occur before updates. 
        state.RequireForUpdate<TerrainSpawner>();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Disable the NewSpawn tag component from the areas we populated in the previous tick
        var newSpawnQuery = SystemAPI.QueryBuilder().WithAll<NewSpawn>().Build();
        state.EntityManager.SetComponentEnabled<NewSpawn>(newSpawnQuery, false);
        
        // Fetch the terrain spawner entity and component
        var terrainSpawner = SystemAPI.GetSingleton<TerrainSpawner>();
        Entity terrainSpawnerEntity = SystemAPI.GetSingletonEntity<TerrainSpawner>();
        
        // Fetch what chunks to spawn this tick
        var toSpawnbuffer = SystemAPI.GetBuffer<TerrainAreasToSpawn>(terrainSpawnerEntity);
        DynamicBuffer<int3> chunksToSpawnBuffer = toSpawnbuffer.Reinterpret<int3>();
        // If there is nothing to spawn, don't :)
        if (chunksToSpawnBuffer.Length == 0)
        {
            return;
        }
        
        NativeArray<int3> chunksToSpawn = chunksToSpawnBuffer.ToNativeArray(Allocator.TempJob);
        if (chunksToSpawnBuffer.Length > terrainSpawner.maxChunkSpawnsPerTick)
        {
            chunksToSpawnBuffer.RemoveRange(0, terrainSpawner.maxChunkSpawnsPerTick);
        }
        else
        {
            chunksToSpawnBuffer.Clear();
        }

        // Spawn the terrain area entities
        state.EntityManager.Instantiate(terrainSpawner.TerrainArea,
            chunksToSpawn.Length > terrainSpawner.maxChunkSpawnsPerTick ? terrainSpawner.maxChunkSpawnsPerTick : chunksToSpawn.Length,
            Allocator.Temp);
        // Then populate them on worker threads
        new PopulateTerrainAreas
        {
            initialAreas = terrainSpawner.initialAreas,
            chunksToSpawn = chunksToSpawn,
            //ecb = parallelEcb,
            noiseSeed = terrainSpawner.seed,
            blocksPerChunkSide = terrainSpawner.blocksPerChunkSide,
            YBounds = terrainSpawner.YBounds,
        }.ScheduleParallel();
    }
}

[WithAll(typeof(NewSpawn))]
[BurstCompile]
partial struct PopulateTerrainAreas : IJobEntity
{
    [DeallocateOnJobCompletion] public NativeArray<int3> chunksToSpawn;

    public int3 initialAreas;
    public int blocksPerChunkSide;

    public int noiseSeed;
    public int2 YBounds; // x is sea level, y is sky level

    public void Execute(Entity entity, [EntityIndexInQuery] int index, ref DynamicBuffer<TerrainBlocks> terrainBlocksBuffer, ref LocalTransform localTransform, ref TerrainArea terrainArea)
    {
        
        int3 chunk = chunksToSpawn[index];
        int areaX = chunk.x * blocksPerChunkSide - (int)(0.5f*initialAreas.x*blocksPerChunkSide);
        int areaY = chunk.y * blocksPerChunkSide - (int)(0.5f*initialAreas.y*blocksPerChunkSide);
        int areaZ = chunk.z * blocksPerChunkSide - (int)(0.5f*initialAreas.z*blocksPerChunkSide);
        // Physically place the area even though its invisible, useful for checking where to bother rendering
        localTransform.Position = new float3(areaX, areaY, areaZ); 
        terrainArea.location = new int3(areaX, areaY, areaZ);
        terrainArea.numBlocks = 0;

        var perLayer = blocksPerChunkSide * blocksPerChunkSide;
        var numBlocks = perLayer * blocksPerChunkSide;
        terrainBlocksBuffer.Resize(numBlocks, NativeArrayOptions.UninitializedMemory);
        DynamicBuffer<int4> terrainBlocks = terrainBlocksBuffer.Reinterpret<int4>();
        for (int block = 0; block < numBlocks; block++)
        {
            var blockX = (block % blocksPerChunkSide);
            var blockY = (block % perLayer) / blocksPerChunkSide;
            var blockZ = (block / perLayer);
            var globalX = areaX + blockX;
            var globalY = areaY + blockY;
            var globalZ = areaZ + blockZ;
            if (globalY > YBounds.x) // Everything beneath sea level exists, so only check if above that
            {
                float noise = CustomPerlin.PerlinGetNoise(globalX, globalZ, noiseSeed);
                var cutoff = YBounds.x + noise * (YBounds.y - YBounds.x);
                if (globalY > cutoff)
                {
                    // "air" terrain
                    terrainBlocks[block] = new int4(-1);
                    continue;
                }
            }
            terrainBlocks[block] = new int4(blockX, blockY, blockZ, 1);
            terrainArea.numBlocks++;
        }
    }
}
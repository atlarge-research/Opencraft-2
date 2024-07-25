using System;
using System.Collections.Generic;
using Opencraft.Terrain;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Layers;
using Opencraft.Terrain.Structures;
using Opencraft.Terrain.Utilities;
using Opencraft.ThirdParty;
using PolkaDOTS;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Unity.Physics.Authoring;

// Annoyingly this assembly directive must be outside the namespace.
[assembly: RegisterGenericJobType(typeof(SortJob<int2, Int2DistanceComparer>))]
namespace Opencraft.Terrain
{


    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    // System that generates new terrain areas based on layering noise over the y axis
    public partial struct TerrainGenerationSystem : ISystem
    {
        private EntityQuery _newSpawnQuery;
        private NativeArray<TerrainGenerationLayer> _terrainGenLayers;
        private ProfilerMarker _markerTerrainGen;
        private ComponentLookup<TerrainArea> _terrainAreaLookup;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private BufferLookup<TerrainBlocks> _terrainBlocksLookup;
        private BufferLookup<BlockLogicState> _terrainLogicStateLookup;
        private BufferLookup<BlockDirection> _terrainDirectionLookup;
        private BufferLookup<TerrainBlockUpdates> _terrainUpdateLookup;
        private BufferLookup<TerrainColMinY> _terrainColMinLookup;
        private BufferLookup<TerrainColMaxY> _terrainColMaxLookup;
        private BufferLookup<TerrainStructuresToSpawn> _structuresToSpawnLookup;
        private int _hashedSeed;
        private NativeArray<Entity> terrainAreasEntities;
        private NativeArray<TerrainArea> terrainAreas;

        //private double lastUpdate;

        public void OnCreate(ref SystemState state)
        {
            // Wait for scene load/baking to occur before updates. 
            state.RequireForUpdate<TerrainSpawner>();
            state.RequireForUpdate<WorldParameters>();
            state.RequireForUpdate<TerrainGenerationLayer>();
            state.RequireForUpdate<TerrainColumnsToSpawn>();
            _newSpawnQuery = SystemAPI.QueryBuilder().WithAll<NewSpawn, TerrainArea, LocalTransform>().Build();
            _markerTerrainGen = new ProfilerMarker("TerrainGeneration");
            //lastUpdate = -1.0;
            _terrainAreaLookup = state.GetComponentLookup<TerrainArea>(isReadOnly: false);
            _localTransformLookup = state.GetComponentLookup<LocalTransform>(isReadOnly: false);
            _terrainBlocksLookup = state.GetBufferLookup<TerrainBlocks>(isReadOnly: false);
            _terrainLogicStateLookup = state.GetBufferLookup<BlockLogicState>(isReadOnly: false);
            _terrainDirectionLookup = state.GetBufferLookup<BlockDirection>(isReadOnly: false);
            _terrainUpdateLookup = state.GetBufferLookup<TerrainBlockUpdates>(isReadOnly: false);
            _terrainColMinLookup = state.GetBufferLookup<TerrainColMinY>(isReadOnly: false);
            _terrainColMaxLookup = state.GetBufferLookup<TerrainColMaxY>(isReadOnly: false);
            _structuresToSpawnLookup = state.GetBufferLookup<TerrainStructuresToSpawn>(isReadOnly: false);

            // Seed
            MD5 md5Hasher = MD5.Create();
            var hashed = md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(ApplicationConfig.Seed.Value));
            _hashedSeed = BitConverter.ToInt32(hashed, 0);

        }

        public void OnDestroy(ref SystemState state)
        {
            _terrainGenLayers.Dispose();
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!_terrainGenLayers.IsCreated)
            {
                _terrainGenLayers = SystemAPI.QueryBuilder().WithAll<TerrainGenerationLayer>().Build().ToComponentDataArray<TerrainGenerationLayer>(Allocator.Persistent);
            }

            var terrainAreasQuery = SystemAPI.QueryBuilder().WithAll<TerrainArea, LocalTransform>().Build();
            terrainAreasEntities = terrainAreasQuery.ToEntityArray(state.WorldUpdateAllocator);
            terrainAreas = terrainAreasQuery.ToComponentDataArray<TerrainArea>(state.WorldUpdateAllocator);

            /*if (state.World.Time.ElapsedTime - lastUpdate < 1.0)
            {
                return;
            }
            lastUpdate = state.World.Time.ElapsedTime;*/

            // Disable the NewSpawn tag component from the areas we populated in the previous tick
            state.EntityManager.SetComponentEnabled<NewSpawn>(_newSpawnQuery, false);

            // Fetch the terrain spawner entity and component
            var terrainSpawner = SystemAPI.GetSingleton<TerrainSpawner>();
            var worldParameters = SystemAPI.GetSingleton<WorldParameters>();
            Entity terrainSpawnerEntity = SystemAPI.GetSingletonEntity<TerrainSpawner>();
            int columnHeight = worldParameters.ColumnHeight;
            int worldHeight = columnHeight * Env.AREA_SIZE;

            // Fetch what chunks to spawn this tick
            var toSpawnbuffer = SystemAPI.GetBuffer<TerrainColumnsToSpawn>(terrainSpawnerEntity);
            DynamicBuffer<int2> chunksColumnsSpawnBuffer = toSpawnbuffer.Reinterpret<int2>();
            // If there is nothing to spawn, don't :)
            if (chunksColumnsSpawnBuffer.Length == 0)
            {
                return;
            }
            _markerTerrainGen.Begin();
            NativeArray<int2> columnsToSpawn = chunksColumnsSpawnBuffer.AsNativeArray();
            // Sort the columns to spawn so ones closer to 0,0 are first
            SortJob<int2, Int2DistanceComparer> sortJob = columnsToSpawn.SortJob<int2, Int2DistanceComparer>(new Int2DistanceComparer { });
            JobHandle sortHandle = sortJob.Schedule(state.Dependency);

            // Spawn the terrain area entities
            int numColumnsToSpawn = columnsToSpawn.Length > Env.MAX_COL_PER_TICK
                ? Env.MAX_COL_PER_TICK
                : columnsToSpawn.Length;
            NativeArray<Entity> terrainAreaEntities = state.EntityManager.Instantiate(terrainSpawner.TerrainArea,
                numColumnsToSpawn * columnHeight,
                Allocator.TempJob);
            _terrainBlocksLookup.Update(ref state);
            _terrainLogicStateLookup.Update(ref state);
            _terrainDirectionLookup.Update(ref state);
            _terrainUpdateLookup.Update(ref state);
            _terrainColMinLookup.Update(ref state);
            _terrainColMaxLookup.Update(ref state);
            _structuresToSpawnLookup.Update(ref state);
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            EntityCommandBuffer.ParallelWriter parallelEcb = ecb.AsParallelWriter();

            // Populate new terrain areas on worker threads
            JobHandle populateHandle = new PopulateTerrainColumns
            {
                terrainAreaEntities = terrainAreaEntities,
                ecb = parallelEcb,
                terrainBlocksLookup = _terrainBlocksLookup,
                terrainLogicStateLookup = _terrainLogicStateLookup,
                terrainDirectionLookup = _terrainDirectionLookup,
                terrainUpdateLookup = _terrainUpdateLookup,
                terrainColMinLookup = _terrainColMinLookup,
                terrainColMaxLookup = _terrainColMaxLookup,
                _structuresToSpawnLookup = _structuresToSpawnLookup,
                columnsToSpawn = columnsToSpawn,
                noiseSeed = _hashedSeed,
                worldHeight = worldHeight,
                columnHeight = columnHeight,
                terrainGenLayers = _terrainGenLayers

            }.Schedule(numColumnsToSpawn, 1, sortHandle); // Each thread gets 1 column
            populateHandle.Complete();
            terrainAreaEntities.Dispose();
            //terrainAreas.Dispose();
            //localTransforms.Dispose();

            // Remove spawned areas from the toSpawn buffer
            if (chunksColumnsSpawnBuffer.Length > Env.MAX_COL_PER_TICK)
                chunksColumnsSpawnBuffer.RemoveRange(0, Env.MAX_COL_PER_TICK);
            else
                chunksColumnsSpawnBuffer.Clear();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            _markerTerrainGen.End();
        }
    }


    // Comparer for sorting locations by distance from zero
    public struct Int2DistanceComparer : IComparer<int2>
    {
        public int Compare(int2 a, int2 b)
        {
            int lSum = math.abs(a.x) + math.abs(a.y);
            int rSum = math.abs(b.x) + math.abs(b.y);
            if (lSum > rSum)
                return 1;
            if (lSum < rSum)
                return -1;
            return 0;
        }
    }

    [BurstCompile]
    partial struct PopulateTerrainColumns : IJobParallelFor
    {

        [ReadOnly] public NativeArray<Entity> terrainAreaEntities;
        [ReadOnly] public NativeArray<int2> columnsToSpawn;

        public EntityCommandBuffer.ParallelWriter ecb;
        [NativeDisableParallelForRestriction] public BufferLookup<TerrainBlocks> terrainBlocksLookup;
        [NativeDisableParallelForRestriction] public BufferLookup<BlockLogicState> terrainLogicStateLookup;
        [NativeDisableParallelForRestriction] public BufferLookup<BlockDirection> terrainDirectionLookup;
        [NativeDisableParallelForRestriction] public BufferLookup<TerrainBlockUpdates> terrainUpdateLookup;
        [NativeDisableParallelForRestriction] public BufferLookup<TerrainColMinY> terrainColMinLookup;
        [NativeDisableParallelForRestriction] public BufferLookup<TerrainColMaxY> terrainColMaxLookup;
        [NativeDisableParallelForRestriction] public BufferLookup<TerrainStructuresToSpawn> _structuresToSpawnLookup;
        public int noiseSeed;
        public int columnHeight;
        public int worldHeight;
        [ReadOnly] public NativeArray<TerrainGenerationLayer> terrainGenLayers;

        [BurstCompile]
        public void Execute(int jobIndex)
        {
            int index = jobIndex * columnHeight;
            int2 columnToSpawn = columnsToSpawn[jobIndex];
            int columnX = columnToSpawn.x * Env.AREA_SIZE;
            int columnZ = columnToSpawn.y * Env.AREA_SIZE;

            // Preprocess terrain generation layers to create noise lookup tables
            NativeArray<NativeArray<float>> terrainLayerLookupTables =
                new NativeArray<NativeArray<float>>(terrainGenLayers.Length, Allocator.Temp);

            NoiseUtilities.NoiseInterpolatorSettings noiseInterpSettings = new NoiseUtilities.NoiseInterpolatorSettings();
            NoiseUtilities.GetNoiseInterpolatorSettings(ref noiseInterpSettings, Env.AREA_SIZE_WITH_PADDING, downsamplingFactor: 2);
            for (int i = 0; i < terrainGenLayers.Length; i++)
            {
                TerrainGenerationLayer terrainGenLayer = terrainGenLayers[i];
                switch (terrainGenLayer.layerType)
                {
                    case LayerType.Absolute:
                    case LayerType.Additive:
                        NativeArray<float> lut = new NativeArray<float>((noiseInterpSettings.size + 1) * (noiseInterpSettings.size + 1), Allocator.Temp);
                        terrainLayerLookupTables[i] = lut;
                        // Generate a lookup table for this column of areas
                        int j = 0;
                        for (int z = 0; z < noiseInterpSettings.size; z++)
                        {
                            float zf = (z << noiseInterpSettings.step) + columnZ;
                            for (int x = 0; x < noiseInterpSettings.size; x++)
                            {
                                float xf = (x << noiseInterpSettings.step) + columnX;
                                lut[j++] = NoiseUtilities.GetNoise(xf, 0.0f, zf, noiseSeed, 1f,
                                    terrainGenLayer.amplitude, terrainGenLayer.exponent, terrainGenLayer.frequency,
                                    FastNoise.NoiseType.Simplex);
                            }
                        }
                        break;
                    case LayerType.Surface:
                        // No noise sampling is performed for a surface layer
                        break;
                    case LayerType.Structure:
                        // Don't need at lookup table as we only sample noise at points that get a structure
                        break;
                    case LayerType.On_Input:
                    case LayerType.Wire:
                    case LayerType.Lamp:
                    case LayerType.Calculated_Layer:
                        break;
                }
            }
            // Arrays of relevant components for entire terrain area column
            NativeArray<DynamicBuffer<BlockType>> terrainBlockBuffers =
                new NativeArray<DynamicBuffer<BlockType>>(columnHeight, Allocator.Temp);
            NativeArray<DynamicBuffer<byte>> colMinBuffers =
                new NativeArray<DynamicBuffer<byte>>(columnHeight, Allocator.Temp);
            NativeArray<DynamicBuffer<byte>> colMaxBuffers =
                new NativeArray<DynamicBuffer<byte>>(columnHeight, Allocator.Temp);
            NativeArray<DynamicBuffer<TerrainStructuresToSpawn>> terrainStructureBuffers =
                new NativeArray<DynamicBuffer<TerrainStructuresToSpawn>>(columnHeight, Allocator.Temp);
            //Preprocess terrain area column
            for (int columnAreaY = 0;
                 columnAreaY < columnHeight;
                 columnAreaY++)
            {
                //Entity
                Entity terrainEntity = terrainAreaEntities[index + columnAreaY];

                //TerrainArea
                int3 chunk = new int3(columnToSpawn.x, columnAreaY, columnToSpawn.y);
                ecb.SetComponent(index + columnAreaY, terrainEntity, new TerrainArea { location = chunk });

                //LocalTransform
                int areaY = columnAreaY * Env.AREA_SIZE;
                ecb.SetComponent(index + columnAreaY, terrainEntity, new LocalTransform { Position = new float3(columnX, areaY, columnZ) });

                // Block buffer
                DynamicBuffer<TerrainBlocks> terrainBlocksBuffer = terrainBlocksLookup[terrainEntity];
                terrainBlocksBuffer.Resize(Env.AREA_SIZE_POW_3, NativeArrayOptions.ClearMemory);
                DynamicBuffer<BlockType> terrainBlocks = terrainBlocksBuffer.Reinterpret<BlockType>();
                terrainBlockBuffers[columnAreaY] = terrainBlocks;

                // Logic Buffer
                DynamicBuffer<BlockLogicState> terrainLogicStateBuffer = terrainLogicStateLookup[terrainEntity];
                terrainLogicStateBuffer.Resize(Env.AREA_SIZE_POW_3, NativeArrayOptions.ClearMemory);

                // Direction Buffer
                DynamicBuffer<BlockDirection> terrainDirectionBuffer = terrainDirectionLookup[terrainEntity];
                terrainDirectionBuffer.Resize(Env.AREA_SIZE_POW_3, NativeArrayOptions.ClearMemory);

                // Terrain area column min buffer
                DynamicBuffer<TerrainColMinY> colMinBuffer = terrainColMinLookup[terrainEntity];
                colMinBuffer.Resize(Env.AREA_SIZE_POW_3, NativeArrayOptions.UninitializedMemory);
                unsafe
                {
                    // Initialize array to a max value for column height
                    UnsafeUtility.MemSet(colMinBuffer.GetUnsafePtr(), (byte)Env.AREA_SIZE, Env.AREA_SIZE_POW_3);
                }

                DynamicBuffer<byte> colMin = colMinBuffer.Reinterpret<byte>();
                colMinBuffers[columnAreaY] = colMin;

                //Terrain area column max buffer
                DynamicBuffer<TerrainColMaxY> colMaxBuffer = terrainColMaxLookup[terrainEntity];
                colMaxBuffer.Resize(Env.AREA_SIZE_POW_3, NativeArrayOptions.ClearMemory);
                DynamicBuffer<byte> colMax = colMaxBuffer.Reinterpret<byte>();
                colMaxBuffers[columnAreaY] = colMax;

                // terrain Structures
                DynamicBuffer<TerrainStructuresToSpawn> structuresToSpawnBuffer = _structuresToSpawnLookup[terrainEntity];
                terrainStructureBuffers[columnAreaY] = structuresToSpawnBuffer;
            }

            // iterate up each global y block column
            int globalX, globalZ;
            int modulo_x = 5;
            int modulo_z = 5;
            for (int z = 0; z < Env.AREA_SIZE; z++)
            {
                globalZ = columnZ + z;
                for (int x = 0; x < Env.AREA_SIZE; x++)
                {
                    globalX = columnX + x;
                    // For each block column in area column, iterate upwards through noise layers
                    int columnAccess = x + z * Env.AREA_SIZE;
                    int heightSoFar = 0; // Start at y = 0
                    int startIndex = TerrainUtilities.BlockLocationToIndex(x, 0, z);

                    for (int i = 0; i < terrainGenLayers.Length; i++)
                    {
                        TerrainGenerationLayer terrainGenerationLayer = terrainGenLayers[i];
                        NativeArray<float> lookupTable = terrainLayerLookupTables[i];
                        //NoiseUtilities.NoiseInterpolatorSettings nis = terrainLayerInterpolateSettings[i];
                        switch (terrainGenerationLayer.layerType)
                        {
                            case LayerType.Absolute:
                                heightSoFar = GenerateAbsoluteLayer(ref terrainBlockBuffers,
                                    ref colMinBuffers, ref colMaxBuffers, noiseInterpSettings, ref lookupTable, x, z, startIndex,
                                    heightSoFar, ref terrainGenerationLayer, columnAccess);
                                break;
                            case LayerType.Additive:
                                heightSoFar = GenerateAdditiveLayer(ref terrainBlockBuffers,
                                    ref colMinBuffers, ref colMaxBuffers, noiseInterpSettings, ref lookupTable, x, z, startIndex,
                                    heightSoFar, ref terrainGenerationLayer, columnAccess);
                                break;
                            case LayerType.Surface:
                                heightSoFar = GenerateSurfaceLayer(ref terrainBlockBuffers,
                                    ref colMinBuffers, ref colMaxBuffers, startIndex,
                                    heightSoFar, ref terrainGenerationLayer, columnAccess);
                                break;
                            case LayerType.Structure:
                                // Structure layers do not immediately change blocks or height. Instead, 
                                // they mark that a structure should be generated at a given position
                                GenerateStructureLayer(ref terrainGenerationLayer, ref terrainStructureBuffers,
                                    index, noiseInterpSettings, x, z, globalX, heightSoFar, globalZ);
                                break;
                            case LayerType.On_Input:
                                heightSoFar = GenerateModuloLayer(ref terrainBlockBuffers,
                                    ref colMinBuffers, ref colMaxBuffers, (x % modulo_x == 0 && z % modulo_z == 0 && x != z) ? 1 : 0, startIndex,
                                    heightSoFar, ref terrainGenerationLayer, columnAccess, index);
                                break;
                            case LayerType.Wire:
                                heightSoFar = GenerateModuloLayer(ref terrainBlockBuffers,
                                    ref colMinBuffers, ref colMaxBuffers, ((x % modulo_x != 0 && z % modulo_z == 0) || (x % modulo_x == 0 && z % modulo_z != 0)) ? 1 : 0, startIndex,
                                    heightSoFar, ref terrainGenerationLayer, columnAccess, index);
                                break;
                            case LayerType.Lamp:
                                heightSoFar = GenerateModuloLayer(ref terrainBlockBuffers,
                                    ref colMinBuffers, ref colMaxBuffers, (x % modulo_x == 0 && z % modulo_z == 0 && x == z) ? 1 : 0, startIndex,
                                    heightSoFar, ref terrainGenerationLayer, columnAccess, index);
                                break;
                            case LayerType.Calculated_Layer:
                                heightSoFar = GenerateCalculatedLayer(ref terrainBlockBuffers,
                                    ref colMinBuffers, ref colMaxBuffers, startIndex,
                                    heightSoFar, ref terrainGenerationLayer, columnAccess, index);
                                break;
                        }
                    }

                }
            }

        }

        private void GenerateStructureLayer(ref TerrainGenerationLayer terrainGenLayer, ref NativeArray<DynamicBuffer<TerrainStructuresToSpawn>> structuresToSpawnBuffers,
            int index, NoiseUtilities.NoiseInterpolatorSettings nis, int localX, int localZ, int globalX, int globalY, int globalZ)
        {
            float chanceAtPos = NoiseUtilities.RandomPrecise(TerrainUtilities.BlockLocationHash(globalX, globalY, globalZ), (byte)noiseSeed);
            //if (globalX == 0 && globalZ == 4)
            //    chanceAtPos = 0.0001f;
            int colY = globalY / Env.AREA_SIZE;
            DynamicBuffer<TerrainStructuresToSpawn> structuresToSpawn = structuresToSpawnBuffers[colY];
            int localY = globalY - (colY * Env.AREA_SIZE);
            if (terrainGenLayer.chance > chanceAtPos)
            {
                // Check that neighbor columns don't have this structure as well
                if (NoiseUtilities.RandomPrecise(TerrainUtilities.BlockLocationHash(globalX + 1, globalY, globalZ), (byte)noiseSeed) > chanceAtPos &&
                    NoiseUtilities.RandomPrecise(TerrainUtilities.BlockLocationHash(globalX - 1, globalY, globalZ), (byte)noiseSeed) > chanceAtPos &&
                    NoiseUtilities.RandomPrecise(TerrainUtilities.BlockLocationHash(globalX, globalY, globalZ + 1), (byte)noiseSeed) > chanceAtPos &&
                    NoiseUtilities.RandomPrecise(TerrainUtilities.BlockLocationHash(globalX, globalY, globalZ - 1), (byte)noiseSeed) > chanceAtPos)
                {

                    int noise = NoiseUtilities.FastFloor(NoiseUtilities.GetNoise(globalX, globalY, globalZ, noiseSeed, 1f,
                        Structure.StructureToNoiseRange(terrainGenLayer.structureType), 1f, 1f,
                        FastNoise.NoiseType.Simplex));
                    // Mark that we need to spawn a structure here
                    structuresToSpawn.Add(new TerrainStructuresToSpawn
                    {
                        basePos = new int3(localX, localY, localZ),
                        structureType = terrainGenLayer.structureType,
                        extentsPos = Structure.StructureToExtents(terrainGenLayer.structureType, negativeBounds: false),
                        extentsNeg = Structure.StructureToExtents(terrainGenLayer.structureType, negativeBounds: true),
                        noise = noise,
                        offset = new int3(0)
                    });
                    ecb.SetComponentEnabled<GenStructures>(index, terrainAreaEntities[index + colY], true);
                }
            }
        }

        private int GenerateAbsoluteLayer(ref NativeArray<DynamicBuffer<BlockType>> terrainBlockBuffers,
            ref NativeArray<DynamicBuffer<byte>> colMinBuffers,
            ref NativeArray<DynamicBuffer<byte>> colMaxBuffers,
            NoiseUtilities.NoiseInterpolatorSettings nis, ref NativeArray<float> lut, int x, int z,
            int blockIndex, int heightSoFar, ref TerrainGenerationLayer terrainGenLayer, int columnAccess)
        {
            // Calculate height to add and sum it with the min height (because the height of this
            // layer should fluctuate between minHeight and minHeight+the max noise)
            int columnTop = terrainGenLayer.minHeight + (int)(NoiseUtilities.Interpolate(nis, x, z, lut));

            // Absolute layers add from the minY and up but if the layer height is lower than
            // the existing terrain there's nothing to add so just return the initial value
            if (columnTop > heightSoFar)
            {
                // set blocks from 
                int start = terrainGenLayer.minHeight > 0 ? terrainGenLayer.minHeight : 0;
                int end = columnTop < worldHeight ? columnTop : worldHeight;
                SetColumnBlocks(ref terrainBlockBuffers, ref colMinBuffers, ref colMaxBuffers, start, end,
                    terrainGenLayer.blockType, blockIndex, columnAccess);

                //Return the new global height of this column
                return end;
            }

            return heightSoFar;
        }

        private int GenerateAdditiveLayer(ref NativeArray<DynamicBuffer<BlockType>> terrainBlockBuffers,
            ref NativeArray<DynamicBuffer<byte>> colMinBuffers,
            ref NativeArray<DynamicBuffer<byte>> colMaxBuffers,
            NoiseUtilities.NoiseInterpolatorSettings nis, ref NativeArray<float> lut, int x, int z,
            int blockIndex, int heightSoFar, ref TerrainGenerationLayer terrainGenLayer, int columnAccess)
        {
            int heightToAdd = terrainGenLayer.minHeight + (int)(NoiseUtilities.Interpolate(nis, x, z, lut));


            int end = heightSoFar + heightToAdd < worldHeight ? heightSoFar + heightToAdd : worldHeight;
            SetColumnBlocks(ref terrainBlockBuffers, ref colMinBuffers, ref colMaxBuffers, heightSoFar, end,
                terrainGenLayer.blockType, blockIndex, columnAccess);

            //Return the new global height of this column
            return end;
        }

        private int GenerateSurfaceLayer(ref NativeArray<DynamicBuffer<BlockType>> terrainBlockBuffers,
            ref NativeArray<DynamicBuffer<byte>> colMinBuffers,
            ref NativeArray<DynamicBuffer<byte>> colMaxBuffers,
            int blockIndex, int heightSoFar, ref TerrainGenerationLayer terrainGenLayer, int columnAccess)
        {
            int heightToAdd = 1;

            int end = heightSoFar + heightToAdd < worldHeight ? heightSoFar + heightToAdd : worldHeight;
            SetColumnBlocks(ref terrainBlockBuffers, ref colMinBuffers, ref colMaxBuffers, heightSoFar, end,
                terrainGenLayer.blockType, blockIndex, columnAccess);

            //Return the new global height of this column
            return end;
        }

        private int GenerateModuloLayer(ref NativeArray<DynamicBuffer<BlockType>> terrainBlockBuffers,
            ref NativeArray<DynamicBuffer<byte>> colMinBuffers,
            ref NativeArray<DynamicBuffer<byte>> colMaxBuffers, int condional,
            int blockIndex, int heightSoFar, ref TerrainGenerationLayer terrainGenLayer, int columnAccess, int index = -1)
        {
            int heightToAdd = condional;

            int end = heightSoFar + heightToAdd < worldHeight ? heightSoFar + heightToAdd : worldHeight;
            SetColumnBlocks(ref terrainBlockBuffers, ref colMinBuffers, ref colMaxBuffers, heightSoFar, end,
            terrainGenLayer.blockType, blockIndex, columnAccess, index);

            return end;
        }
        private int GenerateCalculatedLayer(ref NativeArray<DynamicBuffer<BlockType>> terrainBlockBuffers,
            ref NativeArray<DynamicBuffer<byte>> colMinBuffers,
            ref NativeArray<DynamicBuffer<byte>> colMaxBuffers,
            int blockIndex, int heightSoFar, ref TerrainGenerationLayer terrainGenLayer, int columnAccess, int index = -1)
        {
            BlockType blockType = terrainGenLayer.blockType;
            float add = TerrainUtilities.GetAdd[(int)blockType];
            int3 blockLoc = TerrainUtilities.BlockIndexToLocation(blockIndex);
            int row = blockLoc.x;
            int col = blockLoc.z;

            bool Strips(int row)
            {
                return (row % 2 != 0);
            }

            bool Edges(int row, int col)
            {
                return row == 0 || row == 15 || col == 0 || col == 15;
            }
            bool TwoInputGate(int row, int col, double add)
            {
                return Strips(row) && !Edges(row, col) && (col % 14 == (add + (row + 1) * 0.5) % 14);
            }

            int heightToAdd;
            switch (blockType)
            {
                case BlockType.On_Input:
                    heightToAdd = ((col == 0 || col == 15) && Strips(row) && !(row == 15)) ? 1 : 0;
                    break;
                case BlockType.Clock:
                    heightToAdd = ((row == 15) && (col == 8)) ? 1 : 0;
                    break;
                case BlockType.Off_Wire:
                    heightToAdd = ((Strips(row) && !Edges(row, col) && (col % 6 != (add + (row + 1) * 0.5) % 6)) || (row == 15 && col != 8 && col != 0 && col != 15)) ? 1 : 0;
                    break;
                case BlockType.AND_Gate:
                    heightToAdd = (TwoInputGate(row, col, add)) ? 1 : 0;
                    break;
                case BlockType.OR_Gate:
                    heightToAdd = (TwoInputGate(row, col, add)) ? 1 : 0;
                    break;
                case BlockType.NOT_Gate:
                    heightToAdd = (!Strips(row) && !Edges(row, col) && (col % 6 == (add + (row + 1) * 0.5) % 6)) ? 1 : 0;
                    break;
                case BlockType.XOR_Gate:
                    heightToAdd = (TwoInputGate(row, col, add)) ? 1 : 0;
                    break;
                default:
                    heightToAdd = (false) ? 1 : 0;
                    break;
            }

            int end = heightSoFar + heightToAdd < worldHeight ? heightSoFar + heightToAdd : worldHeight;
            SetColumnBlocks(ref terrainBlockBuffers, ref colMinBuffers, ref colMaxBuffers, heightSoFar, end,
            terrainGenLayer.blockType, blockIndex, columnAccess, index);

            return end;
        }

        // Places blocks in a vertical column. Requires the column of terrain areas.
        private void SetColumnBlocks(ref NativeArray<DynamicBuffer<BlockType>> columnAreaBlockBuffers,
            ref NativeArray<DynamicBuffer<byte>> colMinBuffers,
            ref NativeArray<DynamicBuffer<byte>> colMaxBuffers,
            int start, int end, BlockType blockType, int blockIndex, int columnAccess, int index = -1)
        {
            DynamicBuffer<BlockType> areaBlockBuffer = columnAreaBlockBuffers[0];
            DynamicBuffer<byte> colMinBuffer;
            DynamicBuffer<byte> colMaxBuffer;
            int prevColY = -1;

            // Start = 0, End = 2, prevColY = -1
            for (int globalY = start; globalY < end; globalY++)
            {
                int colY = globalY / Env.AREA_SIZE; // 0
                int chunkYMin = colY * Env.AREA_SIZE; // 0
                int chunkYMax = chunkYMin + Env.AREA_SIZE - 1; // 15
                int localY = globalY - chunkYMin; // 0
                // Check if we have entered a new terrain area
                if (colY != prevColY) // 0 != -1
                {
                    // Get buffers for new terrain area
                    areaBlockBuffer = columnAreaBlockBuffers[colY];
                    colMinBuffer = colMinBuffers[colY];
                    colMaxBuffer = colMaxBuffers[colY];
                    // Set column heightmap
                    if (localY < colMinBuffer[columnAccess])
                        colMinBuffer[columnAccess] = (byte)localY;
                    if (end > colY + chunkYMax)
                        colMaxBuffer[columnAccess] = (byte)(Env.AREA_SIZE);
                    else
                        colMaxBuffer[columnAccess] = (byte)(end - chunkYMin);
                }
                areaBlockBuffer[blockIndex + localY] = blockType;
                if (blockType == BlockType.On_Input)
                {
                    // terrainEntity is the entity of the containing area
                    Entity terrainEntity = terrainAreaEntities[index + colY];
                    DynamicBuffer<bool> blockLogicStates = terrainLogicStateLookup[terrainEntity].Reinterpret<bool>();
                    blockLogicStates[blockIndex + localY] = true;
                }
                if (BlockData.IsInput(blockType) || BlockData.IsGate(blockType))
                {
                    Entity terrainEntity = terrainAreaEntities[index + colY];
                    terrainUpdateLookup[terrainEntity].Add(new TerrainBlockUpdates { blockLoc = TerrainUtilities.BlockIndexToLocation(blockIndex + localY) });
                }
                prevColY = colY;
            }
        }

    }
}

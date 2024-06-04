using Opencraft.Terrain;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Utilities;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using System.Collections.Concurrent;

[assembly: RegisterGenericJobType(typeof(SortJob<int2, Int2DistanceComparer>))]
namespace Opencraft.Terrain
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainStructuresSystem))]
    [BurstCompile]

    public partial struct TerrainPowerSystem : ISystem
    {
        public static ConcurrentDictionary<int3, PowerBlockData> powerBlocks;
        private int tickRate;
        private float timer;
        //private Queue<int3> poweredQueue;
        private BufferLookup<BlockPowered> terrainPowerStateLookup;
        private BufferLookup<TerrainBlocks> terrainBlocksLookup;
        private ComponentLookup<TerrainNeighbors> terrainNeighborsLookup;
        private ComponentLookup<TerrainArea> terrainAreaLookup;
        private static ConcurrentQueue<PowerBlockData> powerQueue;
        static int3[] directions = new int3[] { new int3(-1, 0, 0), new int3(1, 0, 0), new int3(0, -1, 0), new int3(0, 1, 0), new int3(0, 0, -1), new int3(0, 0, 1) };
        static int3 sixteens = new int3(16, 0, 16);

        public struct PowerBlockData
        {
            public int3 BlockLocation;
            public Entity TerrainArea;
        }
        public void OnCreate(ref SystemState state)
        {
            powerBlocks = new ConcurrentDictionary<int3, PowerBlockData>();
            tickRate = 1;
            timer = 0;
            terrainPowerStateLookup = state.GetBufferLookup<BlockPowered>(isReadOnly: false);
            terrainBlocksLookup = state.GetBufferLookup<TerrainBlocks>(isReadOnly: false);
            terrainNeighborsLookup = state.GetComponentLookup<TerrainNeighbors>(isReadOnly: false);
            terrainAreaLookup = state.GetComponentLookup<TerrainArea>(isReadOnly: false);
            powerQueue = new ConcurrentQueue<PowerBlockData>();
        }

        public void OnDestroy(ref SystemState state)
        {
            powerBlocks.Clear();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (timer < tickRate)
            {
                timer += Time.deltaTime;
                return;
            }
            timer = 0;
            terrainNeighborsLookup.Update(ref state);
            terrainBlocksLookup.Update(ref state);
            terrainPowerStateLookup.Update(ref state);
            terrainAreaLookup.Update(ref state);
            powerQueue = new ConcurrentQueue<PowerBlockData>(powerBlocks.Values);
            while (powerQueue.Count > 0)
            {
                powerQueue.TryDequeue(out PowerBlockData poweredBlock);
                PropogatePower(poweredBlock);
            }
        }

        private void PropogatePower(PowerBlockData poweredBlock)
        {
            Entity blockEntity = poweredBlock.TerrainArea;
            int3 blockLoc = poweredBlock.BlockLocation;

            TerrainNeighbors neighbors = terrainNeighborsLookup[blockEntity];
            Entity neighborXN = neighbors.neighborXN;
            Entity neighborXP = neighbors.neighborXP;
            Entity neighborZN = neighbors.neighborZN;
            Entity neighborZP = neighbors.neighborZP;
            Entity[] terrainEntities = new Entity[] { blockEntity, neighborXN, neighborXP, neighborZN, neighborZP };

            for (int i = 0; i < directions.Length; i++)
            {
                int3 notNormalisedBlockLoc = (blockLoc + directions[i]);
                int offsetIndex = GetOffsetIndex(notNormalisedBlockLoc);
                Entity neighborEntity = terrainEntities[offsetIndex];
                if (neighborEntity == Entity.Null) continue;
                int3 neighborBlockLoc = (notNormalisedBlockLoc + sixteens) % 16;
                int blockIndex = TerrainUtilities.BlockLocationToIndex(ref neighborBlockLoc);
                DynamicBuffer<BlockType> blocks = terrainBlocksLookup[neighborEntity].Reinterpret<BlockType>();
                TerrainArea terrainArea = terrainAreaLookup[neighborEntity];
                if (BlockData.PowerableBlock[(int)blocks[blockIndex]])
                {
                    if (terrainPowerStateLookup.TryGetBuffer(neighborEntity, out DynamicBuffer<BlockPowered> terrainPowerState))
                    {
                        if (terrainPowerState[blockIndex].powered == false)
                        {
                            terrainPowerState[blockIndex] = new BlockPowered { powered = true };
                            DynamicBuffer<BlockType> blockTypes = terrainBlocksLookup[neighborEntity].Reinterpret<BlockType>();
                            if (blockTypes[blockIndex] == BlockType.Off_Wire)
                            {
                                powerQueue.Enqueue(new PowerBlockData { BlockLocation = neighborBlockLoc, TerrainArea = neighborEntity });
                            }
                            blockTypes[blockIndex] = blockTypes[blockIndex] + 1;
                            Debug.Log("Powering " + neighborBlockLoc.ToString() + " in area " + terrainArea.location.ToString());
                        }
                    }
                }
            }
        }
        private int GetOffsetIndex(int3 blockLoc)
        {
            switch (blockLoc.x)
            {
                case -1:
                    return 1;
                case 16:
                    return 2;
                default:
                    break;
            }
            switch (blockLoc.z)
            {
                case -1:
                    return 3;
                case 16:
                    return 4;
                default:
                    break;
            }
            return 0;
        }
    }
}
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
using System.Collections.Generic;

[assembly: RegisterGenericJobType(typeof(SortJob<int2, Int2DistanceComparer>))]
namespace Opencraft.Terrain
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainStructuresSystem))]
    [BurstCompile]

    public partial struct TerrainPowerSystem : ISystem
    {
        public static ConcurrentDictionary<int3, LogicBlockData> powerBlocks;
        public static ConcurrentDictionary<int3, LogicBlockData> gateBlocks;
        public static ConcurrentDictionary<int3, LogicBlockData> poweredGateBlocks;
        public static List<LogicBlockData> toDepower;
        private int tickRate;
        private float timer;
        private BufferLookup<BlockPowered> terrainPowerStateLookup;
        private BufferLookup<BlockDirection> terrainDirectionLookup;
        private BufferLookup<TerrainBlocks> terrainBlocksLookup;
        private ComponentLookup<TerrainNeighbors> terrainNeighborsLookup;
        private ComponentLookup<TerrainArea> terrainAreaLookup;
        //private static ConcurrentQueue<PowerBlockData> powerQueue;
        static int3 sixteens = new int3(16, 0, 16);

        public struct LogicBlockData
        {
            public int3 BlockLocation;
            public Entity TerrainArea;
        }
        public void OnCreate(ref SystemState state)
        {
            powerBlocks = new ConcurrentDictionary<int3, LogicBlockData>();
            gateBlocks = new ConcurrentDictionary<int3, LogicBlockData>();
            tickRate = 1;
            timer = 0;
            terrainPowerStateLookup = state.GetBufferLookup<BlockPowered>(isReadOnly: false);
            terrainDirectionLookup = state.GetBufferLookup<BlockDirection>(isReadOnly: false);
            terrainBlocksLookup = state.GetBufferLookup<TerrainBlocks>(isReadOnly: false);
            terrainNeighborsLookup = state.GetComponentLookup<TerrainNeighbors>(isReadOnly: false);
            terrainAreaLookup = state.GetComponentLookup<TerrainArea>(isReadOnly: false);
            toDepower = new List<LogicBlockData>();
        }

        public void OnDestroy(ref SystemState state)
        {
            powerBlocks.Clear();
            gateBlocks.Clear();
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
            terrainDirectionLookup.Update(ref state);
            terrainAreaLookup.Update(ref state);

            //Debug.Log("Ticking Power States");
            PropogatePowerState(toDepower, false);
            toDepower.Clear();
            PropogatePowerState(powerBlocks.Values, true);
        }

        private void PropogatePowerState(ICollection<LogicBlockData> poweredBlocks, bool powerState)
        {
            ConcurrentQueue<LogicBlockData> powerQueue = new ConcurrentQueue<LogicBlockData>(poweredBlocks);
            while (powerQueue.Count > 0)
            {
                powerQueue.TryDequeue(out LogicBlockData poweredBlock);

                Entity blockEntity = poweredBlock.TerrainArea;
                int3 blockLoc = poweredBlock.BlockLocation;

                TerrainNeighbors neighbors = terrainNeighborsLookup[blockEntity];
                Entity neighborXN = neighbors.neighborXN;
                Entity neighborXP = neighbors.neighborXP;
                Entity neighborZN = neighbors.neighborZN;
                Entity neighborZP = neighbors.neighborZP;
                Entity[] terrainEntities = new Entity[] { blockEntity, neighborXN, neighborXP, neighborZN, neighborZP };

                int3[] directions = BlockData.Int3Directions;

                for (int i = 0; i < directions.Length; i++)
                {
                    int3 notNormalisedBlockLoc = (blockLoc + directions[i]);
                    int terrainEntityIndex = GetOffsetIndex(notNormalisedBlockLoc);
                    Entity neighborEntity = terrainEntities[terrainEntityIndex];
                    if (neighborEntity == Entity.Null) continue;

                    int3 actualBlockLoc = (notNormalisedBlockLoc + sixteens) % 16;
                    int blockIndex = TerrainUtilities.BlockLocationToIndex(ref actualBlockLoc);

                    DynamicBuffer<TerrainBlocks> terrainBlocks = terrainBlocksLookup[neighborEntity];
                    DynamicBuffer<BlockType> blockTypes = terrainBlocks.Reinterpret<BlockType>();
                    DynamicBuffer<BlockDirection> blockDirections = terrainDirectionLookup[neighborEntity];
                    DynamicBuffer<BlockPowered> blockPowerState = terrainPowerStateLookup[neighborEntity];
                    DynamicBuffer<bool> boolPowerState = blockPowerState.Reinterpret<bool>();
                    BlockType currentBlock = blockTypes[blockIndex];
                    int currentBlockIndex = (int)currentBlock;

                    if (BlockData.PowerableBlock[currentBlockIndex])
                    {
                        if (boolPowerState[blockIndex] != powerState)
                        {
                            boolPowerState[blockIndex] = powerState;
                            if (currentBlock == BlockType.Off_Wire || currentBlock == BlockType.On_Wire || currentBlock == BlockType.On_Lamp || currentBlock == BlockType.On_Switch || currentBlock == BlockType.Powered_Switch)
                                powerQueue.Enqueue(new LogicBlockData { BlockLocation = actualBlockLoc, TerrainArea = neighborEntity });
                            if (powerState) blockTypes[blockIndex] = (BlockData.PoweredState[currentBlockIndex]);
                            else blockTypes[blockIndex] = (BlockData.DepoweredState[currentBlockIndex]);
                        }
                    }
                }
            }
        }

        private int GetOffsetIndex(int3 blockLoc)
        {
            switch (blockLoc.x)
            {
                case -1: return 1;
                case 16: return 2;
                default: break;
            }
            switch (blockLoc.z)
            {
                case -1: return 3;
                case 16: return 4;
                default: break;
            }
            return 0;
        }
    }
}
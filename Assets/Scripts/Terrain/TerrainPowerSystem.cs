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
using System.Linq;
using Unity.Logging.Internal;
using Unity.VisualScripting;

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
        private double tickRate;
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
            poweredGateBlocks = new ConcurrentDictionary<int3, LogicBlockData>();
            tickRate = 0.33;
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
            poweredGateBlocks.Clear();
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
            IEnumerable<LogicBlockData> poweredBlocks = powerBlocks.Values.Concat(poweredGateBlocks.Values);
            PropogatePowerState(poweredBlocks, true);
            CheckLogicPower(gateBlocks.Values);
        }

        private void PropogatePowerState(IEnumerable<LogicBlockData> poweredBlocks, bool inputPowerState)
        {
            ConcurrentQueue<(LogicBlockData, bool)> powerQueue = new ConcurrentQueue<(LogicBlockData, bool)>();
            foreach (LogicBlockData block in poweredBlocks)
            {
                powerQueue.Enqueue((block, inputPowerState));
            }
            while (powerQueue.Count > 0)
            {
                powerQueue.TryDequeue(out (LogicBlockData, bool) entry);
                LogicBlockData poweredBlock = entry.Item1;
                bool powerState = entry.Item2;

                Entity blockEntity = poweredBlock.TerrainArea;
                int3 blockLoc = poweredBlock.BlockLocation;
                BlockType currentBlockType = terrainBlocksLookup[blockEntity].Reinterpret<BlockType>()[TerrainUtilities.BlockLocationToIndex(ref blockLoc)];
                Direction currentOutputDirection = terrainDirectionLookup[blockEntity].Reinterpret<Direction>()[TerrainUtilities.BlockLocationToIndex(ref blockLoc)];

                TerrainNeighbors neighbors = terrainNeighborsLookup[blockEntity];
                Entity neighborXN = neighbors.neighborXN;
                Entity neighborXP = neighbors.neighborXP;
                Entity neighborZN = neighbors.neighborZN;
                Entity neighborZP = neighbors.neighborZP;
                Entity[] terrainEntities = new Entity[] { blockEntity, neighborXN, neighborXP, neighborZN, neighborZP };

                if (currentBlockType == BlockType.AND_Gate || currentBlockType == BlockType.OR_Gate)
                {
                    EvaluateNeighbour(currentOutputDirection, blockLoc, ref terrainEntities, powerState, ref powerQueue);
                    continue;
                }

                if (currentBlockType == BlockType.NOT_Gate)
                {
                    Direction inputDirection = BlockData.OppositeDirections[(int)currentOutputDirection];
                    int3 notNormalisedBlockLoc = (blockLoc + BlockData.Int3Directions[(int)inputDirection]);
                    int terrainEntityIndex = GetOffsetIndex(notNormalisedBlockLoc);
                    Entity neighborEntity = terrainEntities[terrainEntityIndex];
                    if (neighborEntity == Entity.Null) continue;
                    int lowestCoord = math.min(notNormalisedBlockLoc.x, notNormalisedBlockLoc.z);
                    int num_sixteens = lowestCoord / 16 + 1;
                    int3 actualBlockLoc = (notNormalisedBlockLoc + sixteens * num_sixteens) % 16;
                    int blockIndex = TerrainUtilities.BlockLocationToIndex(ref actualBlockLoc);
                    DynamicBuffer<BlockPowered> blockPowerState = terrainPowerStateLookup[blockEntity];
                    DynamicBuffer<bool> boolPowerState = blockPowerState.Reinterpret<bool>();
                    bool NOTInputPower = boolPowerState[blockIndex];


                    EvaluateNeighbour(currentOutputDirection, blockLoc, ref terrainEntities, !NOTInputPower, ref powerQueue);
                    continue;
                }

                Direction[] allDirections = BlockData.AllDirections;
                for (int i = 0; i < allDirections.Length; i++)
                {
                    Direction outputDirection = allDirections[i];
                    EvaluateNeighbour(outputDirection, blockLoc, ref terrainEntities, powerState, ref powerQueue);
                }
            }
        }

        private void CheckLogicPower(IEnumerable<LogicBlockData> gateBlocks)
        {
            ConcurrentQueue<LogicBlockData> gateQueue = new ConcurrentQueue<LogicBlockData>(gateBlocks);
            while (gateQueue.Count > 0)
            {
                gateQueue.TryDequeue(out LogicBlockData poweredBlock);

                Entity blockEntity = poweredBlock.TerrainArea;
                int3 blockLoc = poweredBlock.BlockLocation;
                int blockIndex = TerrainUtilities.BlockLocationToIndex(ref blockLoc);
                TerrainArea terrainArea = terrainAreaLookup[blockEntity];
                int3 globalPos = terrainArea.location * Env.AREA_SIZE + blockLoc;
                BlockType currentBlockType = terrainBlocksLookup[blockEntity].Reinterpret<BlockType>()[blockIndex];
                DynamicBuffer<BlockPowered> blockPowerState = terrainPowerStateLookup[blockEntity];
                DynamicBuffer<Direction> directionStates = terrainDirectionLookup[blockEntity].Reinterpret<Direction>();
                Direction currentDirection = directionStates[blockIndex];
                DynamicBuffer<bool> boolPowerState = blockPowerState.Reinterpret<bool>();

                Direction[] inputDirections = new Direction[] { };
                GetInputDirections(currentBlockType, ref inputDirections, currentDirection);
                int requiredPower = 0;
                switch (currentBlockType)
                {
                    case BlockType.AND_Gate:
                        requiredPower = 2;
                        break;
                    case BlockType.OR_Gate:
                        requiredPower = 1;
                        break;
                    case BlockType.NOT_Gate:
                        requiredPower = 1;
                        break;
                    default:
                        break;
                }

                TerrainNeighbors neighbors = terrainNeighborsLookup[blockEntity];
                Entity neighborXN = neighbors.neighborXN;
                Entity neighborXP = neighbors.neighborXP;
                Entity neighborZN = neighbors.neighborZN;
                Entity neighborZP = neighbors.neighborZP;
                Entity[] terrainEntities = new Entity[] { blockEntity, neighborXN, neighborXP, neighborZN, neighborZP };

                int powerableBlocks = 0;
                int3[] directions = BlockData.Int3Directions;
                int powerCount = 0;
                for (int i = 0; i < inputDirections.Length; i++)
                {
                    int3 notNormalisedBlockLoc = (blockLoc + directions[(int)inputDirections[i]]);
                    int terrainEntityIndex = GetOffsetIndex(notNormalisedBlockLoc);
                    Entity neighborEntity = terrainEntities[terrainEntityIndex];
                    if (neighborEntity == Entity.Null) continue;

                    int lowestCoord = math.min(notNormalisedBlockLoc.x, notNormalisedBlockLoc.z);
                    int num_sixteens = lowestCoord / 16 + 1;
                    int3 actualBlockLoc = (notNormalisedBlockLoc + sixteens * num_sixteens) % 16;
                    int blockIndex2 = TerrainUtilities.BlockLocationToIndex(ref actualBlockLoc);

                    DynamicBuffer<TerrainBlocks> terrainBlocks = terrainBlocksLookup[neighborEntity];
                    DynamicBuffer<BlockType> blockTypes = terrainBlocks.Reinterpret<BlockType>();
                    DynamicBuffer<BlockPowered> blockPowerState2 = terrainPowerStateLookup[neighborEntity];
                    DynamicBuffer<bool> boolPowerState2 = blockPowerState2.Reinterpret<bool>();
                    BlockType currentBlock = blockTypes[blockIndex2];
                    int currentBlockIndex = (int)currentBlock;

                    if (BlockData.PowerableBlock[currentBlockIndex])
                    {
                        if (boolPowerState2[blockIndex2])
                        {
                            powerCount++;
                        }
                        powerableBlocks++;
                    }
                }
                if (powerCount >= requiredPower || (powerableBlocks == 1 && currentBlockType == BlockType.NOT_Gate))
                {
                    poweredGateBlocks[globalPos] = new LogicBlockData { BlockLocation = blockLoc, TerrainArea = blockEntity };
                    boolPowerState[blockIndex] = true;
                }
                else
                {
                    poweredGateBlocks.TryRemove(globalPos, out LogicBlockData value);
                    toDepower.Add(new LogicBlockData { BlockLocation = blockLoc, TerrainArea = blockEntity });
                }

            }
        }

        private void EvaluateNeighbour(Direction outputDirection, int3 blockLoc, ref Entity[] terrainEntities, bool powerState, ref ConcurrentQueue<(LogicBlockData, bool)> powerQueue)
        {
            int3 direction = BlockData.Int3Directions[(int)outputDirection];
            int3 notNormalisedBlockLoc = (blockLoc + direction);
            int terrainEntityIndex = GetOffsetIndex(notNormalisedBlockLoc);
            Entity neighborEntity = terrainEntities[terrainEntityIndex];
            if (neighborEntity == Entity.Null) return;

            int lowestCoord = math.min(notNormalisedBlockLoc.x, notNormalisedBlockLoc.z);
            int num_sixteens = lowestCoord / 16 + 1;
            int3 actualBlockLoc = (notNormalisedBlockLoc + sixteens * num_sixteens) % 16;
            int blockIndex = TerrainUtilities.BlockLocationToIndex(ref actualBlockLoc);

            DynamicBuffer<TerrainBlocks> terrainBlocks = terrainBlocksLookup[neighborEntity];
            DynamicBuffer<BlockType> blockTypes = terrainBlocks.Reinterpret<BlockType>();
            //DynamicBuffer<BlockDirection> blockDirections = terrainDirectionLookup[neighborEntity];
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
                        powerQueue.Enqueue((new LogicBlockData { BlockLocation = actualBlockLoc, TerrainArea = neighborEntity }, powerState));
                    if (powerState) blockTypes[blockIndex] = (BlockData.PoweredState[currentBlockIndex]);
                    else blockTypes[blockIndex] = (BlockData.DepoweredState[currentBlockIndex]);
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

        private void GetInputDirections(BlockType currentBlockType, ref Direction[] inputDirections, Direction currentDirection)
        {
            switch (currentBlockType)
            {
                case BlockType.AND_Gate:
                case BlockType.OR_Gate:
                    {

                        switch (currentDirection)
                        {
                            case Direction.XN:
                            case Direction.XP:
                                inputDirections = new Direction[] { Direction.ZN, Direction.ZP };
                                break;
                            case Direction.ZN:
                            case Direction.ZP:
                                inputDirections = new Direction[] { Direction.XN, Direction.XP };
                                break;
                            default:
                                break;
                        }
                        break;
                    }
                case BlockType.NOT_Gate:
                    inputDirections = new Direction[] { currentDirection };
                    break;
                default:
                    break;
            }
        }
    }
}
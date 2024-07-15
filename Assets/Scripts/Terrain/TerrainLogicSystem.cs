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
using Unity.Transforms;
using Unity.VisualScripting.FullSerializer;

[assembly: RegisterGenericJobType(typeof(SortJob<int2, Int2DistanceComparer>))]
namespace Opencraft.Terrain
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainStructuresSystem))]
    [BurstCompile]

    public partial struct TerrainLogicSystem : ISystem
    {
        private double tickRate;
        private float timer;
        private static Dictionary<int3, LogicBlockData> inputBlocks;
        private static Dictionary<int3, LogicBlockData> gateBlocks;
        private static Dictionary<int3, LogicBlockData> activeGateBlocks;
        private static List<LogicBlockData> toReevaluate = new List<LogicBlockData>();
        private BufferLookup<BlockLogicState> terrainLogicStateLookup;
        private BufferLookup<BlockDirection> terrainDirectionLookup;
        private BufferLookup<TerrainBlocks> terrainBlocksLookup;
        private BufferLookup<UpdatedBlocks> terrainUpdatedLookup;
        private ComponentLookup<TerrainNeighbors> terrainNeighborsLookup;
        private ComponentLookup<TerrainArea> terrainAreaLookup;
        static int3 sixteens = new int3(16, 0, 16);
        private NativeArray<Entity> terrainAreasEntities;

        public struct LogicBlockData
        {
            public int3 BlockLocation;
            public Entity TerrainEntity;
        }
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainArea>();
            tickRate = 1;
            timer = 0;
            inputBlocks = new Dictionary<int3, LogicBlockData>();
            gateBlocks = new Dictionary<int3, LogicBlockData>();
            activeGateBlocks = new Dictionary<int3, LogicBlockData>();
            terrainLogicStateLookup = state.GetBufferLookup<BlockLogicState>(isReadOnly: false);
            terrainDirectionLookup = state.GetBufferLookup<BlockDirection>(isReadOnly: false);
            terrainBlocksLookup = state.GetBufferLookup<TerrainBlocks>(isReadOnly: false);
            terrainUpdatedLookup = state.GetBufferLookup<UpdatedBlocks>(isReadOnly: false);
            terrainNeighborsLookup = state.GetComponentLookup<TerrainNeighbors>(isReadOnly: false);
            terrainAreaLookup = state.GetComponentLookup<TerrainArea>(isReadOnly: false);
        }

        public void OnDestroy(ref SystemState state)
        {
            inputBlocks.Clear();
            gateBlocks.Clear();
            activeGateBlocks.Clear();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (timer < tickRate)
            {
                timer += Time.deltaTime;
                return;
            }
            timer = 0;
            terrainLogicStateLookup.Update(ref state);
            terrainDirectionLookup.Update(ref state);
            terrainBlocksLookup.Update(ref state);
            terrainUpdatedLookup.Update(ref state);
            terrainNeighborsLookup.Update(ref state);
            terrainAreaLookup.Update(ref state);

            var terrainAreasQuery = SystemAPI.QueryBuilder().WithAll<TerrainArea, LocalTransform>().Build();
            terrainAreasEntities = terrainAreasQuery.ToEntityArray(state.WorldUpdateAllocator);

            foreach (var terrainEntity in terrainAreasEntities)
            {
                DynamicBuffer<int3> updateBlocks = terrainUpdatedLookup[terrainEntity].Reinterpret<int3>();
                if (updateBlocks.Length == 0) continue;
                NativeArray<int3> updateBlocksCopy = updateBlocks.ToNativeArray(Allocator.Temp);
                updateBlocks.Clear();
                DynamicBuffer<BlockType> blockTypeBuffer = terrainBlocksLookup[terrainEntity].Reinterpret<BlockType>();
                TerrainArea terrainArea = terrainAreaLookup[terrainEntity];
                for (int i = 0; i < updateBlocksCopy.Length; i++)
                {
                    int3 blockLoc = updateBlocksCopy[i];
                    int3 globalPos = terrainArea.location * Env.AREA_SIZE + blockLoc;
                    int blockIndex = TerrainUtilities.BlockLocationToIndex(ref blockLoc);
                    BlockType blockType = blockTypeBuffer[blockIndex];

                    LogicBlockData value = new LogicBlockData { BlockLocation = blockLoc, TerrainEntity = terrainEntity };

                    toReevaluate.Add(value);

                    if (blockType == BlockType.Air)
                    {
                        inputBlocks.Remove(blockLoc);
                        gateBlocks.Remove(blockLoc);
                        activeGateBlocks.Remove(blockLoc);
                    }
                    else if (BlockData.IsInput(blockType) || blockType == BlockType.NOT_Gate)
                        inputBlocks.TryAdd(globalPos, value);
                    else if (BlockData.IsGate(blockType))
                        gateBlocks.TryAdd(globalPos, value);
                }
            }

            if (toReevaluate.Count != 0)
            {
                PropagateLogicState(toReevaluate, false);
                toReevaluate.Clear();
            }
            PropagateLogicState(inputBlocks.Values.Concat(activeGateBlocks.Values), true);
            CheckGateState(gateBlocks.Values);

            terrainAreasEntities.Dispose();
        }

        private void PropagateLogicState(IEnumerable<LogicBlockData> logicBlocks, bool inputLogicState)
        {
            ConcurrentQueue<(LogicBlockData, bool)> logicQueue = new ConcurrentQueue<(LogicBlockData, bool)>();
            foreach (LogicBlockData block in logicBlocks)
            {
                logicQueue.Enqueue((block, inputLogicState));
            }
            while (logicQueue.Count > 0)
            {
                logicQueue.TryDequeue(out (LogicBlockData, bool) entry);
                LogicBlockData logicBlock = entry.Item1;
                bool logicState = entry.Item2;

                Entity blockEntity = logicBlock.TerrainEntity;
                int3 blockLoc = logicBlock.BlockLocation;
                int blockIndex = TerrainUtilities.BlockLocationToIndex(ref blockLoc);
                if (terrainBlocksLookup.TryGetBuffer(blockEntity, out DynamicBuffer<TerrainBlocks> terrainBlocks))
                {
                    BlockType currentBlockType = terrainBlocks.Reinterpret<BlockType>()[blockIndex];
                    Direction currentOutputDirection = terrainDirectionLookup[blockEntity].Reinterpret<Direction>()[blockIndex];

                    if (logicState && (currentBlockType == BlockType.Off_Input)) continue;

                    TerrainNeighbors neighbors = terrainNeighborsLookup[blockEntity];
                    Entity neighborXN = neighbors.neighborXN;
                    Entity neighborXP = neighbors.neighborXP;
                    Entity neighborZN = neighbors.neighborZN;
                    Entity neighborZP = neighbors.neighborZP;
                    Entity[] terrainEntities = new Entity[] { blockEntity, neighborXN, neighborXP, neighborZN, neighborZP };


                    if (currentBlockType == BlockType.Clock)
                    {
                        DynamicBuffer<BlockLogicState> blockLogicStates = terrainLogicStateLookup[blockEntity];
                        DynamicBuffer<bool> boolLogicStates = blockLogicStates.Reinterpret<bool>();
                        boolLogicStates[blockIndex] = !boolLogicStates[blockIndex];
                        logicState = boolLogicStates[blockIndex];
                    }

                    if (BlockData.IsTwoInputGate(currentBlockType))
                    {
                        EvaluateNeighbour(currentOutputDirection, blockLoc, ref terrainEntities, logicState, ref logicQueue);
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
                        int blockIndex2 = TerrainUtilities.BlockLocationToIndex(ref actualBlockLoc);
                        DynamicBuffer<BlockLogicState> blockLogicStates = terrainLogicStateLookup[neighborEntity];
                        DynamicBuffer<bool> boolLogicStates = blockLogicStates.Reinterpret<bool>();
                        bool NOTInputState = boolLogicStates[blockIndex2];

                        EvaluateNeighbour(currentOutputDirection, blockLoc, ref terrainEntities, !NOTInputState, ref logicQueue);
                        continue;
                    }

                    Direction[] allDirections = BlockData.AllDirections;
                    for (int i = 0; i < allDirections.Length; i++)
                    {
                        Direction outputDirection = allDirections[i];
                        EvaluateNeighbour(outputDirection, blockLoc, ref terrainEntities, logicState, ref logicQueue);
                    }
                }
            }
        }

        private void CheckGateState(IEnumerable<LogicBlockData> gateBlocks)
        {
            ConcurrentQueue<LogicBlockData> gateQueue = new ConcurrentQueue<LogicBlockData>(gateBlocks);
            while (gateQueue.Count > 0)
            {
                gateQueue.TryDequeue(out LogicBlockData gateBlock);

                Entity blockEntity = gateBlock.TerrainEntity;
                int3 blockLoc = gateBlock.BlockLocation;
                int blockIndex = TerrainUtilities.BlockLocationToIndex(ref blockLoc);
                TerrainArea terrainArea = terrainAreaLookup[blockEntity];
                int3 globalPos = terrainArea.location * Env.AREA_SIZE + blockLoc;
                BlockType currentBlockType = terrainBlocksLookup[blockEntity].Reinterpret<BlockType>()[blockIndex];
                DynamicBuffer<BlockLogicState> blockLogicState = terrainLogicStateLookup[blockEntity];
                DynamicBuffer<Direction> directionStates = terrainDirectionLookup[blockEntity].Reinterpret<Direction>();
                Direction currentDirection = directionStates[blockIndex];
                DynamicBuffer<bool> boolLogicState = blockLogicState.Reinterpret<bool>();

                Direction[] inputDirections = new Direction[] { };
                GetInputDirections(ref inputDirections, currentDirection);
                int requiredInputs = 0;
                switch (currentBlockType)
                {
                    case BlockType.AND_Gate:
                        requiredInputs = 2;
                        break;
                    case BlockType.OR_Gate:
                    case BlockType.XOR_Gate:
                        requiredInputs = 1;
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

                int3[] directions = BlockData.Int3Directions;
                int onCount = 0;
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
                    DynamicBuffer<BlockLogicState> blockLogicStates2 = terrainLogicStateLookup[neighborEntity];
                    DynamicBuffer<bool> boolLogicStates2 = blockLogicStates2.Reinterpret<bool>();
                    BlockType currentBlock = blockTypes[blockIndex2];

                    if (boolLogicStates2[blockIndex2])
                    {
                        onCount++;
                    }
                }

                if ((onCount >= requiredInputs && (currentBlockType == BlockType.AND_Gate || currentBlockType == BlockType.OR_Gate)) || (onCount == requiredInputs && currentBlockType == BlockType.XOR_Gate))
                {
                    boolLogicState[blockIndex] = true;
                    activeGateBlocks.TryAdd(globalPos, gateBlock);
                }
                else
                {
                    boolLogicState[blockIndex] = false;
                    activeGateBlocks.Remove(globalPos);
                    toReevaluate.Add(gateBlock);
                }

            }
        }

        private void EvaluateNeighbour(Direction outputDirection, int3 blockLoc, ref Entity[] terrainEntities, bool logicState, ref ConcurrentQueue<(LogicBlockData, bool)> logicQueue)
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
            DynamicBuffer<BlockLogicState> blockLogicState = terrainLogicStateLookup[neighborEntity];
            DynamicBuffer<bool> boolLogicState = blockLogicState.Reinterpret<bool>();
            BlockType currentBlock = blockTypes[blockIndex];
            int currentBlockIndex = (int)currentBlock;

            if (BlockData.CanReceiveLogic[currentBlockIndex])
            {
                if (boolLogicState[blockIndex] != logicState)
                {
                    boolLogicState[blockIndex] = logicState;
                    if (currentBlock == BlockType.Off_Wire || currentBlock == BlockType.On_Wire || currentBlock == BlockType.On_Lamp)
                        logicQueue.Enqueue((new LogicBlockData { BlockLocation = actualBlockLoc, TerrainEntity = neighborEntity }, logicState));
                    if (logicState) blockTypes[blockIndex] = (BlockData.OnState[currentBlockIndex]);
                    else blockTypes[blockIndex] = (BlockData.OffState[currentBlockIndex]);
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

        private void GetInputDirections(ref Direction[] inputDirections, Direction currentDirection)
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
        }
    }
}
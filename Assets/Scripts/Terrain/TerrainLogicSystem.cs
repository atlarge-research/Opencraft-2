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
using Unity.Profiling;
using Opencraft.Statistics;
using PolkaDOTS;
using UnityEngine.Timeline;
using System;

[assembly: RegisterGenericJobType(typeof(SortJob<int2, Int2DistanceComparer>))]
namespace Opencraft.Terrain
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainStructuresSystem))]
    [BurstCompile]

    public partial struct TerrainLogicSystem : ISystem
    {
        private static bool isRunning = true;
        private double tickRate;
        private float timer;
        private int tickCount;
        private static Dictionary<int3, LogicBlockData> inputBlocks;
        private static Dictionary<int3, LogicBlockData> gateBlocks;
        private static Dictionary<int3, LogicBlockData> activeGateBlocks;
        private static List<LogicBlockData> toReevaluate = new List<LogicBlockData>();
        private BufferLookup<BlockLogicState> terrainLogicStateLookup;
        private BufferLookup<BlockDirection> terrainDirectionLookup;
        private BufferLookup<TerrainBlocks> terrainBlocksLookup;
        private BufferLookup<TerrainBlockUpdates> terrainUpdatedLookup;
        private ComponentLookup<TerrainNeighbors> terrainNeighborsLookup;
        private ComponentLookup<TerrainArea> terrainAreaLookup;
        static int3 sixteens = new int3(16, 0, 16);
        private NativeArray<Entity> terrainAreasEntities;

        public static ProfilerMarker _markerTerrainLogic = new ProfilerMarker("TerrainLogicSystem");

        public static ProfilerMarker GetUpdatesMarker = new ProfilerMarker("GetUpdates");
        public static ProfilerMarker ReevaluatePropagateMarker = new ProfilerMarker("ReevaluatePropagateMarker");
        public static ProfilerMarker PropagateLogicStateMaker = new ProfilerMarker("PropagateLogicState");
        public static ProfilerMarker PropagateLogicStateMaker_1 = new ProfilerMarker("PropInputBlocks");
        public static ProfilerMarker PropagateLogicStateMaker_2 = new ProfilerMarker("PropActiveLogicBlocks");
        public static ProfilerMarker CheckGateStateMarker = new ProfilerMarker("CheckGateState");


        public struct LogicBlockData
        {
            public int3 BlockLocation;
            public Entity TerrainEntity;
        }
        public void OnCreate(ref SystemState state)
        {
            if (!ApplicationConfig.ActiveLogic.Value)
            {
                isRunning = false;
                return;
            }
            state.RequireForUpdate<TerrainArea>();
            tickRate = 1;
            timer = 0;
            tickCount = 0;
            inputBlocks = new Dictionary<int3, LogicBlockData>();
            gateBlocks = new Dictionary<int3, LogicBlockData>();
            activeGateBlocks = new Dictionary<int3, LogicBlockData>();
            terrainLogicStateLookup = state.GetBufferLookup<BlockLogicState>(isReadOnly: false);
            terrainDirectionLookup = state.GetBufferLookup<BlockDirection>(isReadOnly: false);
            terrainBlocksLookup = state.GetBufferLookup<TerrainBlocks>(isReadOnly: false);
            terrainUpdatedLookup = state.GetBufferLookup<TerrainBlockUpdates>(isReadOnly: false);
            terrainNeighborsLookup = state.GetComponentLookup<TerrainNeighbors>(isReadOnly: false);
            terrainAreaLookup = state.GetComponentLookup<TerrainArea>(isReadOnly: false);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (!isRunning) return;
            inputBlocks.Clear();
            gateBlocks.Clear();
            activeGateBlocks.Clear();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!isRunning) return;
            if (timer < tickRate)
            {
                timer += Time.deltaTime;
                return;
            }
            _markerTerrainLogic.Begin();
            timer = 0;
            terrainLogicStateLookup.Update(ref state);
            terrainDirectionLookup.Update(ref state);
            terrainBlocksLookup.Update(ref state);
            terrainUpdatedLookup.Update(ref state);
            terrainNeighborsLookup.Update(ref state);
            terrainAreaLookup.Update(ref state);

            var terrainAreasQuery = SystemAPI.QueryBuilder().WithAll<TerrainArea, LocalTransform>().Build();
            terrainAreasEntities = terrainAreasQuery.ToEntityArray(state.WorldUpdateAllocator);

            GetUpdatesMarker.Begin();
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
            GetUpdatesMarker.End();

            ReevaluatePropagateMarker.Begin();
            if (toReevaluate.Any())
            {
                PropagateLogicState(toReevaluate, false);
                toReevaluate.Clear();
            }
            ReevaluatePropagateMarker.End();

            PropagateLogicStateMaker.Begin();

            PropagateLogicStateMaker_1.Begin();
            PropagateLogicState(inputBlocks.Values, true);
            PropagateLogicStateMaker_1.End();

            PropagateLogicStateMaker_2.Begin();
            PropagateLogicState(activeGateBlocks.Values, true);
            PropagateLogicStateMaker_2.End();

            PropagateLogicStateMaker.End();

            CheckGateStateMarker.Begin();
            CheckGateState(gateBlocks.Values);
            CheckGateStateMarker.End();

            terrainAreasEntities.Dispose();

            GameStatistics.NumInputTypeBlocks.Value = inputBlocks.Count;
            GameStatistics.NumGateTypeBlocks.Value = gateBlocks.Count;
            Debug.Log($"TerrainLogicSystem Tick {++tickCount}");
            _markerTerrainLogic.End();
        }

        private void PropagateLogicState(IEnumerable<LogicBlockData> logicBlocks, bool inputLogicState)
        {
            ConcurrentQueue<(LogicBlockData, bool)> logicQueue = new ConcurrentQueue<(LogicBlockData, bool)>();
            foreach (LogicBlockData block in logicBlocks)
            {
                logicQueue.Enqueue((block, inputLogicState));
            }

            while (logicQueue.TryDequeue(out (LogicBlockData, bool) entry))
            {
                LogicBlockData logicBlock = entry.Item1;
                bool logicState = entry.Item2;

                Entity blockEntity = logicBlock.TerrainEntity;
                int3 blockLoc = logicBlock.BlockLocation;
                int blockIndex = TerrainUtilities.BlockLocationToIndex(ref blockLoc);

                if (!terrainBlocksLookup.TryGetBuffer(blockEntity, out DynamicBuffer<TerrainBlocks> terrainBlocks))
                    continue;

                BlockType currentBlockType = terrainBlocks.Reinterpret<BlockType>()[blockIndex];
                Direction currentOutputDirection = terrainDirectionLookup[blockEntity].Reinterpret<Direction>()[blockIndex];

                if (logicState && currentBlockType == BlockType.Off_Input)
                    continue;

                TerrainNeighbors neighbors = terrainNeighborsLookup[blockEntity];
                Entity[] terrainEntities = { blockEntity, neighbors.neighborXN, neighbors.neighborXP, neighbors.neighborZN, neighbors.neighborZP };

                if (currentBlockType == BlockType.Clock)
                    ToggleClockState(blockEntity, blockIndex, ref logicState);

                if (BlockData.IsTwoInputGate(currentBlockType))
                {
                    EvaluateNeighbour(currentOutputDirection, blockLoc, ref terrainEntities, logicState, ref logicQueue);
                    continue;
                }

                if (currentBlockType == BlockType.NOT_Gate)
                {
                    EvaluateNotGate(currentOutputDirection, blockLoc, ref terrainEntities, ref logicQueue);
                    continue;
                }

                foreach (Direction outputDirection in BlockData.AllDirections)
                    EvaluateNeighbour(outputDirection, blockLoc, ref terrainEntities, logicState, ref logicQueue);
            }
        }

        private void ToggleClockState(Entity blockEntity, int blockIndex, ref bool logicState)
        {
            DynamicBuffer<BlockLogicState> blockLogicStates = terrainLogicStateLookup[blockEntity];
            DynamicBuffer<bool> boolLogicStates = blockLogicStates.Reinterpret<bool>();
            boolLogicStates[blockIndex] = !boolLogicStates[blockIndex];
            logicState = boolLogicStates[blockIndex];
        }

        private void EvaluateNotGate(Direction currentOutputDirection, int3 blockLoc, ref Entity[] terrainEntities, ref ConcurrentQueue<(LogicBlockData, bool)> logicQueue)
        {
            Direction inputDirection = BlockData.OppositeDirections[(int)currentOutputDirection];
            int3 notNormalizedBlockLoc = blockLoc + BlockData.Int3Directions[(int)inputDirection];
            int terrainEntityIndex = GetOffsetIndex(notNormalizedBlockLoc);
            Entity neighborEntity = terrainEntities[terrainEntityIndex];

            if (neighborEntity == Entity.Null) return;

            int3 actualBlockLoc = CalculateActualBlockLocation(notNormalizedBlockLoc);
            int blockIndex2 = TerrainUtilities.BlockLocationToIndex(ref actualBlockLoc);

            DynamicBuffer<BlockLogicState> blockLogicStates = terrainLogicStateLookup[neighborEntity];
            DynamicBuffer<bool> boolLogicStates = blockLogicStates.Reinterpret<bool>();
            bool notInputState = boolLogicStates[blockIndex2];

            EvaluateNeighbour(currentOutputDirection, blockLoc, ref terrainEntities, !notInputState, ref logicQueue);
        }

        private void EvaluateNeighbour(Direction outputDirection, int3 blockLoc, ref Entity[] terrainEntities, bool logicState, ref ConcurrentQueue<(LogicBlockData, bool)> logicQueue)
        {
            int3 direction = BlockData.Int3Directions[(int)outputDirection];
            int3 notNormalisedBlockLoc = (blockLoc + direction);
            int terrainEntityIndex = GetOffsetIndex(notNormalisedBlockLoc);
            Entity neighborEntity = terrainEntities[terrainEntityIndex];
            if (neighborEntity == Entity.Null) return;

            int3 actualBlockLoc = CalculateActualBlockLocation(notNormalisedBlockLoc);
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
        private void CheckGateState(in IEnumerable<LogicBlockData> gateBlocks)
        {
            var gateQueue = new ConcurrentQueue<LogicBlockData>(gateBlocks);

            while (gateQueue.TryDequeue(out var gateBlock))
            {
                Span<Direction> inputDirections = stackalloc Direction[2]; // Assuming max 4 directions
                var (blockEntity, blockLoc) = (gateBlock.TerrainEntity, gateBlock.BlockLocation);
                var blockIndex = TerrainUtilities.BlockLocationToIndex(ref blockLoc);

                if (!terrainAreaLookup.TryGetComponent(blockEntity, out var terrainArea)) continue;
                var globalPos = terrainArea.location * Env.AREA_SIZE + blockLoc;

                var terrainBlocks = terrainBlocksLookup[blockEntity].Reinterpret<BlockType>();
                var currentBlockType = terrainBlocks[blockIndex];
                var blockLogicState = terrainLogicStateLookup[blockEntity];
                var directionStates = terrainDirectionLookup[blockEntity].Reinterpret<Direction>();
                var currentDirection = directionStates[blockIndex];
                var boolLogicState = blockLogicState.Reinterpret<bool>();

                GetInputDirections(ref inputDirections, currentDirection);

                int requiredInputs = currentBlockType switch
                {
                    BlockType.AND_Gate => 2,
                    BlockType.OR_Gate or BlockType.XOR_Gate => 1,
                    _ => 0
                };

                var neighbors = terrainNeighborsLookup[blockEntity];
                var terrainEntities = new[] { blockEntity, neighbors.neighborXN, neighbors.neighborXP, neighbors.neighborZN, neighbors.neighborZP };

                int onCount = CountActiveInputs(inputDirections, blockLoc, terrainEntities);

                bool isActive = (currentBlockType == BlockType.AND_Gate || currentBlockType == BlockType.OR_Gate)
                    ? onCount >= requiredInputs
                    : onCount == requiredInputs; // False scenario is for XOR gate

                boolLogicState[blockIndex] = isActive;

                if (isActive)
                    activeGateBlocks.TryAdd(globalPos, gateBlock);
                else if (activeGateBlocks.Remove(globalPos))
                    toReevaluate.Add(gateBlock);
                inputDirections.Clear();
            }
        }

        private int CountActiveInputs(ReadOnlySpan<Direction> inputDirections, int3 blockLoc, Entity[] terrainEntities)
        {
            int onCount = 0;
            var directions = BlockData.Int3Directions;

            foreach (var inputDirection in inputDirections)
            {
                var notNormalisedBlockLoc = blockLoc + directions[(int)inputDirection];
                var terrainEntityIndex = GetOffsetIndex(notNormalisedBlockLoc);
                var neighborEntity = terrainEntities[terrainEntityIndex];
                if (neighborEntity == Entity.Null) continue;

                var actualBlockLoc = CalculateActualBlockLocation(notNormalisedBlockLoc);
                var blockIndex = TerrainUtilities.BlockLocationToIndex(ref actualBlockLoc);

                if (terrainLogicStateLookup.TryGetBuffer(neighborEntity, out var blockLogicStates))
                {
                    var boolLogicStates = blockLogicStates.Reinterpret<bool>();
                    if (boolLogicStates[blockIndex])
                        onCount++;
                }
            }

            return onCount;
        }

        private int3 CalculateActualBlockLocation(int3 notNormalisedBlockLoc)
        {
            int lowestCoord = math.min(notNormalisedBlockLoc.x, notNormalisedBlockLoc.z);
            int num_sixteens = lowestCoord / 16 + 1;
            return (notNormalisedBlockLoc + 16 * num_sixteens) % 16;
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

        private void GetInputDirections(ref Span<Direction> inputDirections, Direction currentDirection)
        {
            switch (currentDirection)
            {
                case Direction.XN:
                case Direction.XP:
                    inputDirections[0] = Direction.ZN;
                    inputDirections[1] = Direction.ZP;
                    break;
                case Direction.ZN:
                case Direction.ZP:
                    inputDirections[0] = Direction.XN;
                    inputDirections[1] = Direction.XP;
                    break;
                default:
                    break;
            }
        }
    }
}
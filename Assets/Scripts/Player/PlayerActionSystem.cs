using System.Linq;
using Opencraft.Player.Authoring;
using Opencraft.Terrain;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Opencraft.Player
{
    // Handle the terrain modification events specifically
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PlayerActionSystem : ISystem
    {
        private BufferLookup<TerrainBlocks> _terrainBlocksBufferLookup;
        private BufferLookup<BlockLogicState> _terrainPowerStateLookup;
        private BufferLookup<BlockDirection> _terrainDirectionLookup;
        private BufferLookup<TerrainColMinY> _terrainColumnMinBufferLookup;
        private BufferLookup<TerrainColMaxY> _terrainColumnMaxBufferLookup;
        private ComponentLookup<TerrainArea> _terrainAreaLookup;


        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerComponent>();
            state.RequireForUpdate<TerrainArea>();
            state.RequireForUpdate<TerrainSpawner>();
            _terrainBlocksBufferLookup = state.GetBufferLookup<TerrainBlocks>(false);
            _terrainPowerStateLookup = state.GetBufferLookup<BlockLogicState>(false);
            _terrainDirectionLookup = state.GetBufferLookup<BlockDirection>(false);
            _terrainColumnMinBufferLookup = state.GetBufferLookup<TerrainColMinY>(false);
            _terrainColumnMaxBufferLookup = state.GetBufferLookup<TerrainColMaxY>(false);
            _terrainAreaLookup = state.GetComponentLookup<TerrainArea>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _terrainBlocksBufferLookup.Update(ref state);
            _terrainPowerStateLookup.Update(ref state);
            _terrainDirectionLookup.Update(ref state);
            _terrainColumnMinBufferLookup.Update(ref state);
            _terrainColumnMaxBufferLookup.Update(ref state);
            _terrainAreaLookup.Update(ref state);

            foreach (var player in SystemAPI.Query<PlayerAspect>().WithAll<Simulate, PlayerInGame>())
            {
                // Destroy block action
                if (player.Input.PrimaryAction.IsSet && player.SelectedBlock.terrainArea != Entity.Null)
                {
                    Entity terrainAreaEntity = player.SelectedBlock.terrainArea;
                    if (_terrainBlocksBufferLookup.TryGetBuffer(terrainAreaEntity, out DynamicBuffer<TerrainBlocks> terrainBlocks))
                    {
                        int3 blockLoc = player.SelectedBlock.blockLoc;
                        int blockIndex = TerrainUtilities.BlockLocationToIndex(ref blockLoc);
                        int colIndex = TerrainUtilities.BlockLocationToColIndex(ref blockLoc);
                        DynamicBuffer<BlockType> blocks = terrainBlocks.Reinterpret<BlockType>();
                        if (blocks[blockIndex] != BlockType.Air && blocks[blockIndex] != BlockType.Unbreakable)
                        {
                            var colMins = _terrainColumnMinBufferLookup[terrainAreaEntity].Reinterpret<byte>();
                            var colMaxes = _terrainColumnMaxBufferLookup[terrainAreaEntity].Reinterpret<byte>();
                            int minY = colMins[colIndex];
                            int maxY = colMaxes[colIndex];
                            // If removed block is top or bottom of a column, search for new top or bottom and update heightmaps
                            if (blockLoc.y == minY)
                            {
                                // Search upwards for new min y
                                for (int y = 1; y < Env.AREA_SIZE - blockLoc.y; y++)
                                {
                                    if (blocks[blockIndex + y] != BlockType.Air)
                                    {
                                        // found a non-empty block
                                        colMins[colIndex] = (byte)(blockLoc.y + y);
                                        break;
                                    }

                                    if (y == (Env.AREA_SIZE - blockLoc.y) - 1)
                                    {
                                        // no non-empty blocks found, set min to a max value
                                        colMins[colIndex] = (byte)Env.AREA_SIZE;
                                    }
                                }
                            }
                            if (blockLoc.y + 1 == maxY)
                            {
                                // Search downwards for new max
                                for (int y = 1; y < blockLoc.y; y++)
                                {
                                    if (blocks[blockIndex - y] != BlockType.Air)
                                    {
                                        // found a non-empty block
                                        colMaxes[colIndex] = (byte)(blockLoc.y - y + 1);
                                        break;
                                    }

                                    if (y == blockLoc.y - 1)
                                    {
                                        // no non-empty blocks found, set max to a min value
                                        colMaxes[colIndex] = 0;
                                    }
                                }
                            }
                            BlockType destroyedBlockType = blocks[blockIndex];
                            blocks[blockIndex] = BlockType.Air;

                            DynamicBuffer<bool> boolPowerStates = _terrainPowerStateLookup[terrainAreaEntity].Reinterpret<bool>();
                            boolPowerStates[blockIndex] = false;

                            TerrainArea terrainArea = _terrainAreaLookup[terrainAreaEntity];
                            int3 globalPos = terrainArea.location * Env.AREA_SIZE + blockLoc;
                            //UnityEngine.Debug.Log($"globalPos: {globalPos}");

                            if (destroyedBlockType == BlockType.Off_Switch || destroyedBlockType == BlockType.On_Switch)
                                TerrainLogicSystem.RemoveInputBlock(globalPos);
                            if (BlockData.IsGate(destroyedBlockType))
                            {
                                TerrainLogicSystem.RemoveGateBlock(globalPos);
                                TerrainLogicSystem.RemovePoweredGateBlock(globalPos);
                            }
                            if (BlockData.IsPowerTransmitter(destroyedBlockType) || BlockData.IsGate(destroyedBlockType))
                                TerrainLogicSystem.AddReevaluateBlock(blockLoc, terrainAreaEntity);
                        }
                    }
                }

                // Place block action, using the neighbor of selected block
                if (player.Input.SecondaryAction.IsSet && player.SelectedBlock.neighborTerrainArea != Entity.Null)
                {
                    Entity terrainAreaEntity = player.SelectedBlock.neighborTerrainArea;
                    if (_terrainBlocksBufferLookup.TryGetBuffer(terrainAreaEntity, out DynamicBuffer<TerrainBlocks> terrainBlocks))
                    {
                        BlockType blockToPlace = (BlockType)player.Input.SelectedItem;
                        int3 blockLoc = player.SelectedBlock.neighborBlockLoc;
                        int blockIndex = TerrainUtilities.BlockLocationToIndex(ref blockLoc);
                        int colIndex = TerrainUtilities.BlockLocationToColIndex(ref blockLoc);
                        DynamicBuffer<BlockType> blocks = terrainBlocks.Reinterpret<BlockType>();
                        if (blocks[blockIndex] == BlockType.Air)
                        {
                            var colMins = _terrainColumnMinBufferLookup[terrainAreaEntity].Reinterpret<byte>();
                            var colMaxes = _terrainColumnMaxBufferLookup[terrainAreaEntity].Reinterpret<byte>();
                            int minY = colMins[colIndex];
                            int maxY = colMaxes[colIndex];
                            // If new block is the top or bottom of a column, update the column heightmaps
                            if (blockLoc.y < minY)
                                colMins[colIndex] = (byte)blockLoc.y;
                            if (blockLoc.y + 1 > maxY)
                                colMaxes[colIndex] = (byte)(blockLoc.y + 1);
                            blocks[blockIndex] = blockToPlace;
                            TerrainArea terrainArea = _terrainAreaLookup[terrainAreaEntity];
                            int3 globalPos = terrainArea.location * Env.AREA_SIZE + blockLoc;

                            if (_terrainDirectionLookup.TryGetBuffer(terrainAreaEntity, out DynamicBuffer<BlockDirection> blockDirections))
                            {
                                float3 playerPos = player.TransformComponent.Position;
                                blockDirections[blockIndex] = new BlockDirection { direction = GetDirection(playerPos, globalPos) };
                            }

                            if (blockToPlace == BlockType.Off_Switch)
                                TerrainLogicSystem.AddInputBlock(globalPos, blockLoc, player.SelectedBlock.terrainArea);
                            if (BlockData.IsGate(blockToPlace))
                                TerrainLogicSystem.AddGateBlock(globalPos, blockLoc, player.SelectedBlock.terrainArea);
                            if (blockToPlace == BlockType.Off_Wire || blockToPlace == BlockType.Off_Lamp || BlockData.IsGate(blockToPlace))
                                TerrainLogicSystem.AddReevaluateBlock(blockLoc, terrainAreaEntity);

                            DynamicBuffer<bool> blockPowered = _terrainPowerStateLookup[terrainAreaEntity].Reinterpret<bool>();
                            blockPowered[blockIndex] = false;
                        }
                    }
                }

                if (player.Input.ThirdAction.IsSet && player.SelectedBlock.terrainArea != Entity.Null)
                {
                    UnityEngine.Debug.Log("Third action triggered");
                    Entity terrainAreaEntity = player.SelectedBlock.terrainArea;
                    if (_terrainBlocksBufferLookup.TryGetBuffer(terrainAreaEntity, out DynamicBuffer<TerrainBlocks> terrainBlocks))
                    {
                        int3 blockLoc = player.SelectedBlock.blockLoc;
                        int blockIndex = TerrainUtilities.BlockLocationToIndex(ref blockLoc);
                        DynamicBuffer<BlockType> blocks = terrainBlocks.Reinterpret<BlockType>();
                        if (blocks[blockIndex] == BlockType.Off_Switch)
                        {
                            blocks[blockIndex] = BlockType.On_Switch;
                            //DynamicBuffer<bool> blockPowered = _terrainPowerStateLookup[terrainAreaEntity].Reinterpret<bool>();
                            //blockPowered[blockIndex] = true;
                        }
                        else if (blocks[blockIndex] == BlockType.On_Switch)
                        {
                            blocks[blockIndex] = BlockType.Off_Switch;
                            TerrainLogicSystem.AddReevaluateBlock(blockLoc, terrainAreaEntity);

                            //DynamicBuffer<bool> blockPowered = _terrainPowerStateLookup[terrainAreaEntity].Reinterpret<bool>();
                            //blockPowered[blockIndex] = false;
                        }
                    }
                }
            }
        }
        private Direction GetDirection(float3 playerPos, int3 globalPos)
        {
            playerPos = new float3(playerPos.x, NoiseUtilities.FastFloor(playerPos.y), playerPos.z);
            float3 offset = globalPos - playerPos;
            float3 absoluteOffset = new(math.abs(offset.x), math.abs(offset.y), math.abs(offset.z));
            Direction dir = Direction.XP;
            if (absoluteOffset.x > absoluteOffset.z)
            {
                if (offset.x > 0) dir = Direction.XN;
                else dir = Direction.XP;
            }
            else if (absoluteOffset.z > absoluteOffset.x)
            {
                if (offset.z > 0) dir = Direction.ZN;
                else dir = Direction.ZP;
            }
            return dir;
        }
    }
}
using Opencraft.Player.Authoring;
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
        private BufferLookup<TerrainColMinY> _terrainColumnMinBufferLookup;
        private BufferLookup<TerrainColMaxY> _terrainColumnMaxBufferLookup;
        private NativeArray<Entity> terrainAreasEntities;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {

            state.RequireForUpdate<Authoring.Player>();
            state.RequireForUpdate<TerrainArea>();
            state.RequireForUpdate<TerrainSpawner>();
            _terrainBlocksBufferLookup = state.GetBufferLookup<TerrainBlocks>(false);
            _terrainColumnMinBufferLookup= state.GetBufferLookup<TerrainColMinY>(false);
            _terrainColumnMaxBufferLookup= state.GetBufferLookup<TerrainColMaxY>(false);

        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _terrainBlocksBufferLookup.Update(ref state);
            _terrainColumnMinBufferLookup.Update(ref state);
            _terrainColumnMaxBufferLookup.Update(ref state);
            var terrainAreasQuery = SystemAPI.QueryBuilder().WithAll<TerrainArea, LocalTransform>().Build();
            terrainAreasEntities = terrainAreasQuery.ToEntityArray(state.WorldUpdateAllocator);

            foreach (var player in SystemAPI.Query<PlayerAspect>().WithAll<Simulate>())
            {
                // Destroy block action
                if (player.Input.PrimaryAction.IsSet && player.SelectedBlock.terrainAreaIndex != -1)
                {
                    
                    Entity terrainAreaEntity = terrainAreasEntities[player.SelectedBlock.terrainAreaIndex];
                    if(_terrainBlocksBufferLookup.TryGetBuffer(terrainAreaEntity, out DynamicBuffer<TerrainBlocks> terrainBlocks))
                    {
                        int3 blockLoc = player.SelectedBlock.blockLoc;
                        int blockIndex = TerrainUtilities.BlockLocationToIndex(ref blockLoc);
                        int colIndex = TerrainUtilities.BlockLocationToColIndex(ref blockLoc);
                        DynamicBuffer<BlockType> blocks = terrainBlocks.Reinterpret<BlockType>();
                        if (blocks[blockIndex] != BlockType.Air)
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
                                for (int y = 1; y < blockLoc.y ; y++)
                                {
                                    if (blocks[blockIndex - y] != BlockType.Air)
                                    {
                                        // found a non-empty block
                                        colMaxes[colIndex] = (byte)(blockLoc.y-y+1);
                                        break;
                                    }

                                    if (y == blockLoc.y-1)
                                    {
                                        // no non-empty blocks found, set max to a min value
                                        colMaxes[colIndex] = 0;
                                    }
                                }
                            }
                            blocks[blockIndex] = BlockType.Air;
                        }
                    }
                    
                }
                // Place block action, using the neighbor of selected block
                if (player.Input.SecondaryAction.IsSet && player.SelectedBlock.neighborTerrainAreaIndex != -1)
                {
                    Entity terrainAreaEntity = terrainAreasEntities[player.SelectedBlock.neighborTerrainAreaIndex];
                    if(_terrainBlocksBufferLookup.TryGetBuffer(terrainAreaEntity, out DynamicBuffer<TerrainBlocks> terrainBlocks))
                    {
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
                                colMins[colIndex] = (byte) blockLoc.y;
                            if (blockLoc.y + 1 > maxY)
                                colMaxes[colIndex] = (byte) (blockLoc.y + 1);
                            // todo ability to place something other than stone
                            blocks[blockIndex] = BlockType.Stone;
                        }
                    }
                }
            }

        }
    }
}
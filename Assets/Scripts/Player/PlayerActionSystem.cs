using Opencraft.Player.Authoring;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Opencraft.Player
{
    // Handle the terrain modification events specifically
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PlayerActionSystem : ISystem
    {
        private BufferLookup<TerrainBlocks> _terrainBlockLookup;
        private NativeArray<Entity> terrainAreasEntities;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {

            state.RequireForUpdate<Authoring.Player>();
            state.RequireForUpdate<TerrainArea>();
            state.RequireForUpdate<TerrainSpawner>();
            _terrainBlockLookup = state.GetBufferLookup<TerrainBlocks>(false);
            
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _terrainBlockLookup.Update(ref state);
            var terrainAreasQuery = SystemAPI.QueryBuilder().WithAll<TerrainArea, LocalTransform>().Build();
            terrainAreasEntities = terrainAreasQuery.ToEntityArray(state.WorldUpdateAllocator);

            foreach (var player in SystemAPI.Query<PlayerAspect>().WithAll<Simulate>())
            {
                // Destroy block action
                if (player.Input.PrimaryAction.IsSet && player.SelectedBlock.terrainAreaIndex != -1)
                {
                    
                    Entity terrainAreaEntity = terrainAreasEntities[player.SelectedBlock.terrainAreaIndex];
                    if(_terrainBlockLookup.TryGetBuffer(terrainAreaEntity, out DynamicBuffer<TerrainBlocks> terrainBlocks))
                    {
                        int blockIndex = TerrainUtilities.BlockLocationToIndex(ref player.SelectedBlock.blockLoc);
                        DynamicBuffer<int> blocks = terrainBlocks.Reinterpret<int>();
                        if (blocks[blockIndex] != -1)
                        {
                            blocks[blockIndex] = -1;
                        }
                    }
                    
                }
                // Place block action, using the neighbor of selected block
                if (player.Input.SecondaryAction.IsSet && player.SelectedBlock.neighborTerrainAreaIndex != -1)
                {
                    Entity terrainAreaEntity = terrainAreasEntities[player.SelectedBlock.neighborTerrainAreaIndex];
                    if(_terrainBlockLookup.TryGetBuffer(terrainAreaEntity, out DynamicBuffer<TerrainBlocks> terrainBlocks))
                    {
                        int blockIndex = TerrainUtilities.BlockLocationToIndex(ref player.SelectedBlock.neighborBlockLoc);
                        DynamicBuffer<int> blocks = terrainBlocks.Reinterpret<int>();
                        if (blocks[blockIndex] == -1)
                        {
                            blocks[blockIndex] = 1;
                        }
                    }
                }
            }

        }
    }
}
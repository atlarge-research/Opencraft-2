using Opencraft.Player.Authoring;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Opencraft.Player
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    // For every player, calculate what block, if any, they have selected
    public partial struct PlayerSelectedBlockSystem : ISystem
    {
        private BufferLookup<TerrainBlocks> _terrainBlockLookup;
        private NativeArray<Entity> terrainAreasEntities;
        private NativeArray<LocalTransform> terrainAreaTransforms;
        private int raycastLength;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {

            state.RequireForUpdate<Authoring.Player>();
            state.RequireForUpdate<TerrainArea>();
            state.RequireForUpdate<TerrainSpawner>();
            _terrainBlockLookup = state.GetBufferLookup<TerrainBlocks>(true);
            raycastLength = 4;

        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _terrainBlockLookup.Update(ref state);
            var terrainAreasQuery = SystemAPI.QueryBuilder().WithAll<TerrainArea, LocalTransform>().Build();
            terrainAreasEntities = terrainAreasQuery.ToEntityArray(state.WorldUpdateAllocator);
            terrainAreaTransforms = terrainAreasQuery.ToComponentDataArray<LocalTransform>(state.WorldUpdateAllocator);
            
            
            foreach (var player in SystemAPI.Query<PlayerAspect>().WithAll<Simulate>())
            {
                // Use player input Yaw/Pitch to calculate the camera direction on clients
                var cameraRot =  math.mul(quaternion.RotateY(player.Input.Yaw),
                    quaternion.RotateX(-player.Input.Pitch));
                var direction = math.mul(cameraRot, math.forward());
                var playerPos = player.Transform.ValueRO.Position;
                int neighborTerrainAreaIndex = -1;
                int3 neighborBlockLoc = new int3(-1);
                
                player.SelectedBlock.blockLoc = new int3(-1);
                player.SelectedBlock.terrainAreaIndex = -1;
                player.SelectedBlock.neighborBlockLoc = new int3(-1);
                player.SelectedBlock.neighborTerrainAreaIndex = -1;
                // Step along a ray from the players position in the direction their camera is looking
                for (int i = 0; i < raycastLength; i++)
                {
                    float3 location = playerPos + (direction * i);
                    if (TerrainUtilities.GetBlockLocationAtPosition(ref location,
                            ref terrainAreaTransforms,
                            out int terrainAreaIndex,
                            out int3 blockLoc))
                    {
                        Entity terrainAreaEntity = terrainAreasEntities[terrainAreaIndex];
                        if (_terrainBlockLookup.TryGetBuffer(terrainAreaEntity,
                                out DynamicBuffer<TerrainBlocks> terrainBlocks))
                        {
                            int blockIndex = TerrainUtilities.BlockLocationToIndex(ref blockLoc);
                            if (terrainBlocks[blockIndex].type != BlockType.Air)
                            {
                                // found selected block
                                player.SelectedBlock.blockLoc = blockLoc;
                                player.SelectedBlock.terrainAreaIndex = terrainAreaIndex;
                                // Set neighbor
                                player.SelectedBlock.neighborBlockLoc = neighborBlockLoc;
                                player.SelectedBlock.neighborTerrainAreaIndex = neighborTerrainAreaIndex;
                                
                                break;
                            }
                            // If this block is air, still mark it as the neighbor
                            neighborTerrainAreaIndex = terrainAreaIndex;
                            neighborBlockLoc = blockLoc;
                            
                        }
                    }
                }
            }
            

        }
    }
}
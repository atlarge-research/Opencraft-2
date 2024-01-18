using Opencraft.Player.Authoring;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Utilities;
using PolkaDOTS;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


namespace Opencraft.Player
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    // For every player, calculate what block, if any, they have selected
    public partial struct PlayerSelectedBlockSystem : ISystem
    {
        private BufferLookup<TerrainBlocks> _terrainBlockLookup;
        private ComponentLookup<TerrainNeighbors> _terrainNeighborLookup;
        private static readonly int raycastLength = 5;
        private static readonly float3 camOffset = new float3(0, Env.CAMERA_Y_OFFSET, 0);
        
        // Reusable block search input/output structs
        private TerrainUtilities.BlockSearchInput BSI;
        private TerrainUtilities.BlockSearchOutput BSO;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {

            state.RequireForUpdate<PolkaDOTS.Player>();
            state.RequireForUpdate<TerrainArea>();
            state.RequireForUpdate<TerrainSpawner>();
            _terrainBlockLookup = state.GetBufferLookup<TerrainBlocks>(true);
            _terrainNeighborLookup = state.GetComponentLookup<TerrainNeighbors>(true);
            
            TerrainUtilities.BlockSearchInput.DefaultBlockSearchInput(ref BSI);
            TerrainUtilities.BlockSearchOutput.DefaultBlockSearchOutput(ref BSO);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _terrainBlockLookup.Update(ref state);
            _terrainNeighborLookup.Update(ref state);
            
            foreach (var player in SystemAPI.Query<PlayerAspect>().WithAll<Simulate, PlayerInGame>())
            {
                player.SelectedBlock.blockLoc = new int3(-1);
                player.SelectedBlock.terrainArea = Entity.Null;
                player.SelectedBlock.neighborBlockLoc = new int3(-1);
                player.SelectedBlock.neighborTerrainArea = Entity.Null;

                if (player.ContainingArea.Area == Entity.Null)
                    continue;
                
                // Use player input Yaw/Pitch to calculate the camera direction on clients
                var cameraRot =  math.mul(quaternion.RotateY(player.Input.Yaw),
                    quaternion.RotateX(-player.Input.Pitch));
                var direction = math.mul(cameraRot, math.forward());
                //var cameraPos = player.Transform.ValueRO.Position + camOffset ;
                Entity neighborTerrainArea = Entity.Null;
                int3 neighborBlockLoc = new int3(-1);
                

                // Setup search inputs
                TerrainUtilities.BlockSearchInput.DefaultBlockSearchInput(ref BSI);
                BSI.basePos = NoiseUtilities.FastFloor(player.Transform.ValueRO.Position);
                BSI.areaEntity = player.ContainingArea.Area;
                BSI.terrainAreaPos = player.ContainingArea.AreaLocation;
                
                // Step along a ray from the players position in the direction their camera is looking
                for (int i = 0; i < raycastLength; i++)
                {
                    //float3 location = cameraPos + (direction * i);
                    TerrainUtilities.BlockSearchOutput.DefaultBlockSearchOutput(ref BSO);
                    BSI.offset = NoiseUtilities.FastFloor(camOffset + (direction * i));
                    if (TerrainUtilities.GetBlockAtPositionByOffset(in BSI, ref BSO,
                            ref _terrainNeighborLookup, ref _terrainBlockLookup))
                    {
                        if (BSO.blockType != BlockType.Air)
                        {
                            // found selected block
                            player.SelectedBlock.blockLoc = BSO.localPos;
                            player.SelectedBlock.terrainArea = BSO.containingArea ;
                            // Set neighbor
                            player.SelectedBlock.neighborBlockLoc = neighborBlockLoc;
                            player.SelectedBlock.neighborTerrainArea = neighborTerrainArea;
                                
                            break;
                        }
                        // If this block is air, still mark it as the neighbor
                        neighborTerrainArea = BSO.containingArea;
                        neighborBlockLoc = BSO.localPos;
                    }
                    
                }
            }
            

        }
    }
}
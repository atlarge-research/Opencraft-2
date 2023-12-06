using Opencraft.Player.Authoring;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Utilities;
using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Collections;
using Unity.NetCode;
using Unity.Transforms;

namespace Opencraft.Player
{

    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [BurstCompile]
    partial struct PlayerMovementSystem : ISystem
    {

        private ProfilerMarker m_MarkerGroundCheck;
        private ProfilerMarker m_MarkerStep;

        // Terrain structure references
        private BufferLookup<TerrainBlocks> _terrainBlockLookup;
        private ComponentLookup<TerrainNeighbors> _terrainNeighborLookup;
        private NativeArray<Entity> terrainAreasEntities;
        private NativeArray<TerrainArea> terrainAreas;
        
        // Static offsets defining player size when used for collision and checking ground support
        private NativeHashSet<float3> _playerSupportOffsets;
        private NativeHashSet<float3> _playerCollisionOffsets;
        
        // Reusable block search input/output structs
        private TerrainUtilities.BlockSearchInput BSI;
        private TerrainUtilities.BlockSearchOutput BSO;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainSpawner>();
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PolkaDOTS.Player>();

            m_MarkerGroundCheck = new ProfilerMarker("GroundCheck");
            m_MarkerStep = new ProfilerMarker("CollisionStep");
            _terrainBlockLookup = state.GetBufferLookup<TerrainBlocks>(true);
            _terrainNeighborLookup = state.GetComponentLookup<TerrainNeighbors>(true);
            float d = 0.25f;
            _playerSupportOffsets = new NativeHashSet<float3>(4, Allocator.Persistent);
            _playerSupportOffsets .Add(new float3(d,-1.1f,d));
            _playerSupportOffsets .Add(new float3(d,-1.1f,-d));
            _playerSupportOffsets .Add(new float3(-d,-1.1f,-d));
            _playerSupportOffsets .Add(new float3(-d,-1.1f,d));
            
            _playerCollisionOffsets= new NativeHashSet<float3>(8, Allocator.Persistent);
            _playerCollisionOffsets.Add(new float3(d,0f,d));
            _playerCollisionOffsets.Add(new float3(d,0f,-d));
            _playerCollisionOffsets.Add(new float3(-d,0f,-d));
            _playerCollisionOffsets.Add(new float3(-d,0f,d));
            _playerCollisionOffsets.Add(new float3(d,-1.0f,d));
            _playerCollisionOffsets.Add(new float3(d,-1.0f,-d));
            _playerCollisionOffsets.Add(new float3(-d,-1.0f,-d));
            _playerCollisionOffsets.Add(new float3(-d,-1.0f,d));
            
            TerrainUtilities.BlockSearchInput.DefaultBlockSearchInput(ref BSI);
            TerrainUtilities.BlockSearchOutput.DefaultBlockSearchOutput(ref BSO);
        }

        public void OnDestroy(ref SystemState state)
        {
            _playerSupportOffsets.Dispose();
            _playerCollisionOffsets.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            
            var movementSpeed = SystemAPI.Time.DeltaTime * 6;
            SystemAPI.TryGetSingleton<ClientServerTickRate>(out var tickRate);
            tickRate.ResolveDefaults();
            // Make the jump arc look the same regardless of simulation tick rate
            var velocityDecrementStep = 60 / tickRate.SimulationTickRate;
            
            _terrainBlockLookup.Update(ref state);
            _terrainNeighborLookup.Update(ref state);
            var terrainAreasQuery = SystemAPI.QueryBuilder().WithAll<TerrainArea, LocalTransform>().Build();
            terrainAreasEntities = terrainAreasQuery.ToEntityArray(state.WorldUpdateAllocator);
            terrainAreas = terrainAreasQuery.ToComponentDataArray<TerrainArea>(state.WorldUpdateAllocator);

            foreach (var player in SystemAPI.Query<PlayerAspect>().WithAll<Simulate>())
            {
                if (!player.AutoCommandTarget.Enabled)
                {
                    return;
                }
                

                float3 pos = player.Transform.ValueRO.Position;
                
                m_MarkerGroundCheck.Begin();
                // Check the terrain areas underneath the player
                int containingAreaIndex = GetPlayerContainingArea(pos, out int3 containingAreaLoc);
                PlayerSupportState supportState = PlayerSupportState.Unsupported;
                if (containingAreaIndex != -1)
                {
                    player.ContainingArea.Area = terrainAreasEntities[containingAreaIndex];
                    player.ContainingArea.AreaLocation = containingAreaLoc;
                    supportState =
                        CheckPlayerSupported(player.ContainingArea.Area, containingAreaLoc, player.Transform.ValueRO.Position);
                }
                else
                {
                    player.ContainingArea.Area = Entity.Null;
                    player.ContainingArea.AreaLocation = new int3(-1);
                }
                m_MarkerGroundCheck.End();

                // Simple jump mechanism, when jump event is set the jump velocity is set
                // then on each tick it is decremented. It results in an input value being set either
                // in the upward or downward direction (just like left/right movement).
                if (supportState == PlayerSupportState.Supported && player.Input.Jump.IsSet)
                {
                    // Allow jump and stop falling when grounded
                    player.Player.JumpVelocity = 20;
                }
                
                var verticalMovement = 0f;
                if (player.Player.JumpVelocity > 0)
                {
                    player.Player.JumpVelocity -= velocityDecrementStep;
                    verticalMovement = 1;
                }
                else
                {
                    // If jumpvelocity is low enough start moving down again when unsupported
                    if (supportState == PlayerSupportState.Unsupported)
                        verticalMovement = -1;
                }
                
                float2 input = player.Input.Movement;
                float3 wantedMove = new float3(input.x, verticalMovement, input.y);
                
                wantedMove = math.normalizesafe(wantedMove) * movementSpeed;
                
                // Wanted movement is relative to camera
                wantedMove = math.rotate(quaternion.RotateY(player.Input.Yaw), wantedMove);
                // Keep track of rotations
                player.Player.Pitch = player.Input.Pitch;
                player.Player.Yaw = player.Input.Yaw;
                

                m_MarkerStep.Begin();

                MovePlayerCheckCollisions(SystemAPI.Time.DeltaTime, ref pos, ref wantedMove, player.ContainingArea.Area, containingAreaLoc);
                
                m_MarkerStep.End();


                player.Transform.ValueRW.Position = pos;
            }
        }
        
        [BurstCompile]
        // Checks if the blocks under a player exist in the terrain
        private int GetPlayerContainingArea(float3 pos, out int3 containingAreaLoc)
        {
            var playerAreaLoc = TerrainUtilities.GetContainingAreaLocation(ref pos);
            
            if (!TerrainUtilities.GetTerrainAreaByPosition(ref playerAreaLoc, in terrainAreas, out int containingAreaIndex))
            {
                containingAreaLoc = new int3(-1);
                return -1;
            }

            containingAreaLoc = playerAreaLoc;
            return containingAreaIndex;
            
        }

        [BurstCompile]
        // Checks if the blocks under a player exist in the terrain
        private PlayerSupportState CheckPlayerSupported(Entity containingArea, int3 containingAreaLoc, float3 pos)
        {
            // Setup search inputs
            TerrainUtilities.BlockSearchInput.DefaultBlockSearchInput(ref BSI);
            BSI.offset = int3.zero;
            BSI.areaEntity = containingArea;
            BSI.terrainAreaPos = containingAreaLoc;
            // Check corners under player
            foreach (var offset in _playerSupportOffsets)
            {
                TerrainUtilities.BlockSearchOutput.DefaultBlockSearchOutput(ref BSO);
                BSI.basePos = NoiseUtilities.FastFloor(pos + offset);
                
                if (TerrainUtilities.GetBlockAtPositionByOffset(in BSI, ref BSO,
                        ref _terrainNeighborLookup, ref _terrainBlockLookup))
                {
                    if(BSO.blockType != BlockType.Air)
                        return PlayerSupportState.Supported;
                }

            }
            return PlayerSupportState.Unsupported;

        }


        [BurstCompile]
        // Checks if there are blocks in the way of the player's movement
        private void MovePlayerCheckCollisions(float deltaTime, ref float3 pos, ref float3 wantedMove,
            Entity containingArea, int3 containingAreaLoc)
        {
            float3 newPosition = pos + wantedMove;
            // Setup search inputs
            TerrainUtilities.BlockSearchInput.DefaultBlockSearchInput(ref BSI);
            BSI.offset = int3.zero;
            BSI.areaEntity = containingArea;
            BSI.terrainAreaPos = containingAreaLoc;

            if (containingArea == Entity.Null)
            {
                pos = newPosition;
                return;
            }
            
            foreach (var offset in _playerCollisionOffsets)
            {
                TerrainUtilities.BlockSearchOutput.DefaultBlockSearchOutput(ref BSO);
                BSI.basePos = NoiseUtilities.FastFloor(newPosition + offset);

                if (TerrainUtilities.GetBlockAtPositionByOffset(in BSI, ref BSO,
                        ref _terrainNeighborLookup, ref _terrainBlockLookup))
                {
                    if (BSO.blockType != BlockType.Air)
                    {
                        newPosition = pos + new float3(0, wantedMove.y, 0);
                        pos = newPosition;
                        return;
                    }
                }

            }
            
            pos = newPosition;
            
        }
        
        public enum PlayerSupportState : byte
        {
            Unsupported = 0,
            Sliding,
            Supported
        }
    }
}





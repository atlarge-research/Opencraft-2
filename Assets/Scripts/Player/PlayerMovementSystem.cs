using Opencraft.Player.Authoring;
using Opencraft.Player.Utilities;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Utilities;
using Unity.Entities;
using Unity.Burst;
//using Unity.Physics;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Collections;
using Unity.Entities.Content;
using Unity.NetCode;
//using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace Opencraft.Player
{
    //[UpdateInGroup(typeof(PhysicsSystemGroup))]
    //[UpdateBefore(typeof(PhysicsInitializeGroup))]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [BurstCompile]
    // Player movement system using Unity physics tooling, allows players to interact with physics objects
    // Adapted from https://github.com/Unity-Technologies/EntityComponentSystemSamples/blob/master/NetcodeSamples/Assets/Samples/HelloNetcode/2_Intermediate/01_CharacterController/CharacterControllerSystem.cs
    partial struct PlayerMovementSystem : ISystem
    {
        const float  gravity = 9.82f;
        const float  jumpspeed = 5;
        const float  speed = 5;

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
            //state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<Authoring.Player>();

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
            
            // We need the Unity physics to be ready
            //var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            //if (!HasPhysicsWorldBeenInitialized(physicsWorldSingleton))
            //    return;
            
            //var networkTime = SystemAPI.GetSingleton<NetworkTime>();
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
            //terrainAreaTransforms = terrainAreasQuery.ToComponentDataArray<LocalTransform>(state.WorldUpdateAllocator);

            foreach (var player in SystemAPI.Query<PlayerAspect>().WithAll<Simulate>())
            {
                if (!player.AutoCommandTarget.Enabled)
                {
                    //player.Velocity.Linear = float3.zero;
                    return;
                }

                //Using local position here is fine, because the character controller does not have any parent.
                //Using the Position is wrong because it is not up to date. (the LocalTransform is synchronized but
                //the world transform isn't).
                /*RigidTransform ccTransform = new RigidTransform()
                {
                    pos = player.Transform.ValueRO.Position,
                    rot = quaternion.identity
                };*/

                float3 pos = player.Transform.ValueRO.Position;


                m_MarkerGroundCheck.Begin();
                // Check the terrain areas underneath the player
                int containingAreaIndex = GetPlayerContainingArea(ref player.Player, pos, out int3 containingAreaLoc);
                PlayerUtilities.PlayerSupportState supportState = PlayerUtilities.PlayerSupportState.Unsupported;
                if (containingAreaIndex != -1)
                {
                    player.Player.ContainingArea = terrainAreasEntities[containingAreaIndex];
                    player.Player.ContainingAreaLocation = containingAreaLoc;
                    supportState =
                        CheckPlayerSupported(ref player.Player, containingAreaLoc, player.Transform.ValueRO.Position);
                }
                else
                {
                    player.Player.ContainingArea = Entity.Null;
                    player.Player.ContainingAreaLocation = new int3(-1);
                }

                m_MarkerGroundCheck.End();

                // Simple jump mechanism, when jump event is set the jump velocity is set to 10
                // then on each tick it is decremented. It results in an input value being set either
                // in the upward or downward direction (just like left/right movement).
                if (supportState == PlayerUtilities.PlayerSupportState.Supported && player.Input.Jump.IsSet)
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
                    if (supportState == PlayerUtilities.PlayerSupportState.Unsupported)
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

                MovePlayerCheckCollisions(SystemAPI.Time.DeltaTime, ref pos, ref wantedMove, ref player.Player, containingAreaLoc);
                
                m_MarkerStep.End();


                player.Transform.ValueRW.Position = pos;
            }
        }

        /// <summary>
        /// As we run before <see cref="PhysicsInitializeGroup"/> it is possible to execute before any physics bodies
        /// has been initialized.
        ///
        /// </summary>
        /*static bool HasPhysicsWorldBeenInitialized(PhysicsWorldSingleton physicsWorldSingleton)
        {
            return physicsWorldSingleton.PhysicsWorld.Bodies is { IsCreated: true, Length: > 0 };
        }*/
        
        [BurstCompile]
        // Checks if the blocks under a player exist in the terrain
        private int GetPlayerContainingArea(ref Authoring.Player player, float3 pos, out int3 containingAreaLoc)
        {
            var playerAreaLoc = TerrainUtilities.GetContainingAreaLocation(ref pos);
            
            if (!TerrainUtilities.GetTerrainAreaByPosition(ref playerAreaLoc, in terrainAreas, out int containingAreaIndex))
            {
                //OpencraftLogger.Log($"Player {player.Username} at {transform.pos} corresponding to {playerAreaLoc} not contained in an area!");
                containingAreaLoc = new int3(-1);
                return -1;
            }

            containingAreaLoc = playerAreaLoc;
            return containingAreaIndex;
            
        }

        [BurstCompile]
        // Checks if the blocks under a player exist in the terrain
        private PlayerUtilities.PlayerSupportState CheckPlayerSupported(ref Authoring.Player player, int3 containingAreaLoc, float3 pos)
        {
            // Setup search inputs
            TerrainUtilities.BlockSearchInput.DefaultBlockSearchInput(ref BSI);
            BSI.basePos = pos;
            BSI.areaEntity = player.ContainingArea;
            BSI.terrainAreaPos = containingAreaLoc;
            // Check corners under player
            foreach (var offset in _playerSupportOffsets)
            {
                TerrainUtilities.BlockSearchOutput.DefaultBlockSearchOutput(ref BSO);
                BSI.offset = offset;
                
                if (TerrainUtilities.GetBlockAtPositionByOffset(in BSI, ref BSO,
                        ref _terrainNeighborLookup, ref _terrainBlockLookup))
                {
                    if(BSO.blockType != BlockType.Air)
                        return PlayerUtilities.PlayerSupportState.Supported;
                }

            }
            return PlayerUtilities.PlayerSupportState.Unsupported;

        }


        [BurstCompile]
        // Checks if there are blocks in the way of the player's movement
        private void MovePlayerCheckCollisions(float deltaTime, ref float3 pos, ref float3 wantedMove,
            ref Authoring.Player player, int3 containingAreaLoc)
        {
            float3 newPosition = pos + wantedMove;
            // Setup search inputs
            TerrainUtilities.BlockSearchInput.DefaultBlockSearchInput(ref BSI);
            BSI.basePos = newPosition;
            BSI.areaEntity = player.ContainingArea;
            BSI.terrainAreaPos = containingAreaLoc;

            if (player.ContainingArea == Entity.Null)
            {
                pos = newPosition;
                return;
            }
            
            foreach (var offset in _playerCollisionOffsets)
            {
                TerrainUtilities.BlockSearchOutput.DefaultBlockSearchOutput(ref BSO);
                BSI.offset = offset;

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
    }
}





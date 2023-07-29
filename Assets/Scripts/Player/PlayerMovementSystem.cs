using Opencraft.Player.Authoring;
using Opencraft.Player.Utilities;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Utilities;
using Unity.Entities;
using Unity.Burst;
using Unity.Physics;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Collections;
using Unity.Entities.Content;
using Unity.NetCode;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace Opencraft.Player
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(PhysicsInitializeGroup))]
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

        private BufferLookup<TerrainBlocks> _terrainBlockLookup;
        private ComponentLookup<TerrainNeighbors> _terrainNeighborLookup;
        private NativeArray<Entity> terrainAreasEntities;
        //private NativeArray<LocalTransform> terrainAreaTransforms;
        private NativeArray<TerrainArea> terrainAreas;
        private NativeHashSet<float3> _playerSupportOffsets;
        private NativeHashSet<float3> _playerCollisionOffsets;
        
        // Reusable block search input/output structs
        private TerrainUtilities.BlockSearchInput BSI;
        private TerrainUtilities.BlockSearchOutput BSO;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainSpawner>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
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
            var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            if (!HasPhysicsWorldBeenInitialized(physicsWorldSingleton))
                return;
            
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();

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
                    player.Velocity.Linear = float3.zero;
                    return;
                }

                //Using local position here is fine, because the character controller does not have any parent.
                //Using the Position is wrong because it is not up to date. (the LocalTransform is synchronized but
                //the world transform isn't).
                RigidTransform ccTransform = new RigidTransform()
                {
                    pos = player.Transform.ValueRO.Position,
                    rot = quaternion.identity
                };


                m_MarkerGroundCheck.Begin();
                // Check the terrain areas underneath the player
                int containingAreaIndex = GetPlayerContainingArea(ref player.Player, ccTransform, out int3 containingAreaLoc);
                PlayerUtilities.PlayerSupportState supportState = PlayerUtilities.PlayerSupportState.Unsupported;
                if (containingAreaIndex != -1)
                {
                    player.Player.ContainingArea = terrainAreasEntities[containingAreaIndex];
                    player.Player.ContainingAreaLocation = containingAreaLoc;
                    supportState =
                        CheckPlayerSupported(ref player.Player, containingAreaLoc, ccTransform);
                }
                else
                {
                    player.Player.ContainingArea = Entity.Null;
                    player.Player.ContainingAreaLocation = new int3(-1);
                }

                m_MarkerGroundCheck.End();

                float2 input = player.Input.Movement;
                float3 wantedMove = new float3(input.x, 0, input.y) * speed * SystemAPI.Time.DeltaTime;

                // Wanted movement is relative to camera
                wantedMove = math.rotate(quaternion.RotateY(player.Input.Yaw), wantedMove);

                float3 wantedVelocity = wantedMove / SystemAPI.Time.DeltaTime;
                wantedVelocity.y = player.Player.Velocity.y;
                player.Player.Velocity = wantedVelocity;
                
                if (supportState == PlayerUtilities.PlayerSupportState.Supported)
                {
                    player.Player.JumpStart = NetworkTick.Invalid;
                    player.Player.OnGround = 1;
                    // Allow jump and stop falling when grounded
                    if (player.Input.Jump.IsSet)
                    {
                        player.Player.Velocity.y = jumpspeed;
                        player.Player.JumpStart = networkTime.ServerTick;
                    }
                    else
                        player.Player.Velocity.y = 0;
                }
                else
                {
                    player.Player.OnGround = 0;
                    // Free fall
                    player.Player.Velocity.y -= gravity * SystemAPI.Time.DeltaTime;
                }

                m_MarkerStep.Begin();
                // Ok because affect bodies is false so no impulses are written
                // todo add collision distance integration
                MovePlayerCheckCollisions(SystemAPI.Time.DeltaTime, ref ccTransform, ref player.Player, containingAreaLoc);
                //NativeStream.Writer deferredImpulseWriter = default;
                //PlayerUtilities.CollideAndIntegrate(stepInput, k_DefaultMass, false, ref playerCollider, ref ccTransform, ref player.Player.Velocity, ref deferredImpulseWriter);
                m_MarkerStep.End();

                // Set the physics velocity and let physics move the kinematic object based on that
                player.Velocity.Linear =
                    (ccTransform.pos - player.Transform.ValueRO.Position) / SystemAPI.Time.DeltaTime;
            }
        }

        /// <summary>
        /// As we run before <see cref="PhysicsInitializeGroup"/> it is possible to execute before any physics bodies
        /// has been initialized.
        ///
        /// There may be a better way to do this.
        /// </summary>
        static bool HasPhysicsWorldBeenInitialized(PhysicsWorldSingleton physicsWorldSingleton)
        {
            return physicsWorldSingleton.PhysicsWorld.Bodies is { IsCreated: true, Length: > 0 };
        }
        
        [BurstCompile]
        // Checks if the blocks under a player exist in the terrain
        private int GetPlayerContainingArea(ref Authoring.Player player, RigidTransform transform, out int3 containingAreaLoc)
        {
            var playerAreaLoc = TerrainUtilities.GetContainingAreaLocation(ref transform.pos);
            
            if (!TerrainUtilities.GetTerrainAreaByPosition(ref playerAreaLoc, in terrainAreas, out int containingAreaIndex))
            {
                //Debug.LogWarning($"Player {player.Username} at {transform.pos} corresponding to {playerAreaLoc} not contained in an area!");
                containingAreaLoc = new int3(-1);
                return -1;
            }

            containingAreaLoc = playerAreaLoc;
            return containingAreaIndex;
            
        }

        [BurstCompile]
        // Checks if the blocks under a player exist in the terrain
        private PlayerUtilities.PlayerSupportState CheckPlayerSupported(ref Authoring.Player player, int3 containingAreaLoc, RigidTransform transform)
        {
            // Setup search inputs
            TerrainUtilities.BlockSearchInput.DefaultBlockSearchInput(ref BSI);
            BSI.basePos = transform.pos;
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
        private void MovePlayerCheckCollisions(float deltaTime, ref RigidTransform transform,
            ref Authoring.Player player, int3 containingAreaLoc)
        {
            float3 newPosition = transform.pos + deltaTime * player.Velocity;
            // Setup search inputs
            TerrainUtilities.BlockSearchInput.DefaultBlockSearchInput(ref BSI);
            BSI.basePos = newPosition;
            BSI.areaEntity = player.ContainingArea;
            BSI.terrainAreaPos = containingAreaLoc;

            if (player.ContainingArea == Entity.Null)
            {
                transform.pos = newPosition;
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
                        newPosition = transform.pos + deltaTime * new float3(0, player.Velocity.y, 0);
                        transform.pos = newPosition;
                        return;
                    }
                }

            }
            
            transform.pos = newPosition;
            
        }
    }
}





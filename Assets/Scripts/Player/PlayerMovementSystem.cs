using Opencraft.Player.Authoring;
using Opencraft.Player.Utilities;
using Opencraft.Terrain.Authoring;
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
        const float k_DefaultTau = 0.4f;
        const float k_DefaultDamping = 0.9f;
        const float k_DefaultSkinWidth = 0f;
        const float k_DefaultContactTolerance = 0.1f;
        const float k_DefaultMaxSlope = 60f;
        const float k_DefaultMaxMovementSpeed = 10f;
        const int k_DefaultMaxIterations = 10;
        const float k_DefaultMass = 1f;

        private ProfilerMarker m_MarkerGroundCheck;
        private ProfilerMarker m_MarkerStep;

        private BufferLookup<TerrainBlocks> _terrainBlockLookup;
        private NativeArray<Entity> terrainAreasEntities;
        private NativeArray<LocalTransform> terrainAreaTransforms;
        private int blocksPerChunkSide;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainSpawner>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<Authoring.Player>();

            m_MarkerGroundCheck = new ProfilerMarker("GroundCheck");
            m_MarkerStep = new ProfilerMarker("Step");
            _terrainBlockLookup = state.GetBufferLookup<TerrainBlocks>(true);
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
            var terrainAreasQuery = SystemAPI.QueryBuilder().WithAll<TerrainArea, LocalTransform>().Build();
            terrainAreasEntities = terrainAreasQuery.ToEntityArray(state.WorldUpdateAllocator);
            terrainAreaTransforms = terrainAreasQuery.ToComponentDataArray<LocalTransform>(state.WorldUpdateAllocator);
            blocksPerChunkSide = SystemAPI.GetSingleton<TerrainSpawner>().blocksPerSide;

            foreach (var player in SystemAPI.Query<PlayerAspect>().WithAll<Simulate>())
            {
                if (!player.AutoCommandTarget.Enabled)
                {
                    player.Velocity.Linear = float3.zero;
                    return;
                }

                //var playerConfig = SystemAPI.GetComponent<PlayerConfig>(player.Player.PlayerConfig);
                //var playerCollider = SystemAPI.GetComponent<PhysicsCollider>(player.Player.PlayerConfig);
                var gravity = 9.82f;
                var jumpspeed = 5;
                var speed = 5;
                // Character step input
                PlayerUtilities.PlayerStepInput stepInput = new PlayerUtilities.PlayerStepInput
                {
                    PhysicsWorldSingleton = physicsWorldSingleton,
                    DeltaTime = SystemAPI.Time.DeltaTime,
                    Up = new float3(0, 1, 0),
                    Gravity = new float3(0, -gravity, 0),
                    MaxIterations = k_DefaultMaxIterations,
                    Tau = k_DefaultTau,
                    Damping = k_DefaultDamping,
                    SkinWidth = k_DefaultSkinWidth,
                    ContactTolerance = k_DefaultContactTolerance,
                    MaxSlope = math.radians(k_DefaultMaxSlope),
                    RigidBodyIndex = physicsWorldSingleton.PhysicsWorld.GetRigidBodyIndex(player.Self),
                    CurrentVelocity = player.Player.Velocity,
                    MaxMovementSpeed = k_DefaultMaxMovementSpeed
                };

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
                PlayerUtilities.PlayerSupportState supportState = CheckPlayerSupported(ccTransform);
                /*PlayerUtilities.CheckSupport(
                    in physicsWorldSingleton,
                    ref playerCollider,
                    stepInput,
                    ccTransform,
                    out PlayerUtilities.PlayerSupportState supportState,
                    out _,
                    out _);*/
                m_MarkerGroundCheck.End();

                float2 input = player.Input.Movement;
                float3 wantedMove = new float3(input.x, 0, input.y) * speed * SystemAPI.Time.DeltaTime;

                // Wanted movement is relative to camera
                wantedMove = math.rotate(quaternion.RotateY(player.Input.Yaw), wantedMove);

                float3 wantedVelocity = wantedMove / SystemAPI.Time.DeltaTime;
                wantedVelocity.y = player.Player.Velocity.y;

                if (supportState == PlayerUtilities.PlayerSupportState.Supported)
                {
                    player.Player.JumpStart = NetworkTick.Invalid;
                    player.Player.OnGround = 1;
                    player.Player.Velocity = wantedVelocity;
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
                MovePlayerCheckCollisions(stepInput, ref ccTransform, ref player.Player.Velocity);
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
        private PlayerUtilities.PlayerSupportState CheckPlayerSupported(RigidTransform transform)
        {
            // Check corners under player
            float offset = 0.5f;
            NativeHashSet<float3> set = new NativeHashSet<float3>(4, Allocator.Temp);
            set.Add(new float3(
                transform.pos.x + offset,
                transform.pos.y - 1.1f,
                transform.pos.z + offset));
            set.Add(new float3(
                transform.pos.x + offset,
                transform.pos.y - 1.1f,
                transform.pos.z - offset));
            set.Add(new float3(
                transform.pos.x - offset,
                transform.pos.y - 1.1f,
                transform.pos.z - offset));
            set.Add(new float3(
                transform.pos.x - offset,
                transform.pos.y - 1.1f,
                transform.pos.z + offset));

            foreach (var pos in set)
            {
                if (TerrainUtilities.GetBlockAtPosition(pos,
                        ref terrainAreasEntities,
                        ref terrainAreaTransforms,
                        ref _terrainBlockLookup,
                        out int _))
                {
                    return PlayerUtilities.PlayerSupportState.Supported;
                }
            }

            return PlayerUtilities.PlayerSupportState.Unsupported;
        }


        [BurstCompile]
        // Checks if there are blocks in the way of the player's movement
        private void MovePlayerCheckCollisions(PlayerUtilities.PlayerStepInput stepInput, ref RigidTransform transform,
            ref float3 linearVelocity)
        {
            float deltaTime = stepInput.DeltaTime;
            float3 newPosition = transform.pos + deltaTime * linearVelocity;


            float3 newVelocity = linearVelocity;
            float3 norm = math.normalize(linearVelocity);

            float offset = 0.5f;
            NativeHashSet<float3> set = new NativeHashSet<float3>(4, Allocator.Temp);
            set.Add(new float3(
                newPosition.x + offset,
                newPosition.y - 1.0f,
                newPosition.z + offset));
            set.Add(new float3(
                newPosition.x + offset,
                newPosition.y - 1.0f,
                newPosition.z - offset));
            set.Add(new float3(
                newPosition.x - offset,
                newPosition.y - 1.0f,
                newPosition.z - offset));
            set.Add(new float3(
                newPosition.x - offset,
                newPosition.y - 1.0f,
                newPosition.z + offset));

            foreach (var pos in set)
                if (TerrainUtilities.GetBlockAtPosition(pos,
                        ref terrainAreasEntities,
                        ref terrainAreaTransforms,
                        ref _terrainBlockLookup,
                        out int _))
                {
                    linearVelocity = new float3(0, 0, 0);
                    return;
                }


            transform.pos = newPosition;
            linearVelocity = newVelocity;

        }
    }
}





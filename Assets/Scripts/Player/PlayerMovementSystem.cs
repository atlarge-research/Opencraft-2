using Opencraft.Player.Authoring;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Utilities;
using PolkaDOTS;
using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Unity.NetCode;
using Unity.Transforms;

namespace Opencraft.Player
{

    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [BurstCompile]
    partial struct PlayerMovementSystem : ISystem
    {
        // Terrain structure references
        private BufferLookup<TerrainBlocks> _terrainBlockLookup;
        private ComponentLookup<TerrainNeighbors> _terrainNeighborLookup;
        private NativeArray<Entity> terrainAreasEntities;
        private NativeArray<TerrainArea> terrainAreas;
        
        // Static offsets defining player size when used for collision and checking ground support
        private NativeHashSet<float3> _playerSupportOffsets;
        private NativeHashSet<float3> _playerCollisionOffsets;
        
        // World generation information
        private int _columnHeight;
        

        public void OnCreate(ref SystemState state)
        {
            if (state.WorldUnmanaged.IsClient() && ApplicationConfig.DisablePrediction.Value)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TerrainSpawner>();
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PlayerComponent>();
            state.RequireForUpdate<WorldParameters>();
            
            _terrainBlockLookup = state.GetBufferLookup<TerrainBlocks>(true);
            _terrainNeighborLookup = state.GetComponentLookup<TerrainNeighbors>(true);
            float d = 0.25f;
            _playerSupportOffsets = new NativeHashSet<float3>(4, Allocator.Persistent);
            _playerSupportOffsets.Add(new float3(d,-1.2f,d));
            _playerSupportOffsets.Add(new float3(d,-1.2f,-d));
            _playerSupportOffsets.Add(new float3(-d,-1.2f,-d));
            _playerSupportOffsets.Add(new float3(-d,-1.2f,d));
            
            _playerCollisionOffsets= new NativeHashSet<float3>(8, Allocator.Persistent);
            _playerCollisionOffsets.Add(new float3(d,0f,d));
            _playerCollisionOffsets.Add(new float3(d,0f,-d));
            _playerCollisionOffsets.Add(new float3(-d,0f,-d));
            _playerCollisionOffsets.Add(new float3(-d,0f,d));
            _playerCollisionOffsets.Add(new float3(d,-1f,d));
            _playerCollisionOffsets.Add(new float3(d,-1f,-d));
            _playerCollisionOffsets.Add(new float3(-d,-1f,-d));
            _playerCollisionOffsets.Add(new float3(-d,-1f,d));

            _columnHeight = -1;
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
            
            // Fetch world generation information from the WorldParameters singleton
            if (_columnHeight == -1)
            {
                var worldParameters = SystemAPI.GetSingleton<WorldParameters>();
                _columnHeight = worldParameters.ColumnHeight;
            }
            
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

            new MovePlayerJob()
            { 
                terrainBlockLookup = _terrainBlockLookup,
                terrainNeighborLookup = _terrainNeighborLookup,
                terrainAreasEntities = terrainAreasEntities,
                terrainAreas = terrainAreas,
                playerSupportOffsets = _playerSupportOffsets,
                playerCollisionOffsets = _playerCollisionOffsets,
                columnHeight = _columnHeight,
                velocityDecrementStep = velocityDecrementStep,
                movementSpeed = movementSpeed
            }.ScheduleParallel();
            
            /*foreach (var player in SystemAPI.Query<PlayerAspect>().WithAll<Simulate, PlayerInGame>())
            {
                if (!player.AutoCommandTarget.Enabled)
                {
                    return;
                }
                

                float3 pos = player.Transform.ValueRO.Position;
                
                
                // Check the terrain areas underneath the player
                int containingAreaIndex = GetPlayerContainingArea(pos, out int3 containingAreaLoc);
                PlayerSupportState supportState = PlayerSupportState.Unsupported;
                int supportedY = -1;
                if (containingAreaIndex != -1)
                {
                    player.ContainingArea.Area = terrainAreasEntities[containingAreaIndex];
                    player.ContainingArea.AreaLocation = containingAreaLoc;
                    supportState =
                        CheckPlayerSupported(player.ContainingArea.Area, containingAreaLoc, player.Transform.ValueRO.Position, ref supportedY);
                }
                else
                {
                    player.ContainingArea.Area = Entity.Null;
                    player.ContainingArea.AreaLocation = new int3(-1);
                }
                

                // Simple jump mechanism, when jump event is set the jump velocity is set
                // then on each tick it is decremented. It results in an input value being set either
                // in the upward or downward direction (just like left/right movement).
                if (supportState == PlayerSupportState.Supported && player.Input.Jump.IsSet)
                {
                    // Allow jump and stop falling when grounded
                    player.PlayerComponent.JumpVelocity = 20;
                }
                
                var verticalMovement = 0f;
                if (player.PlayerComponent.JumpVelocity > 0)
                {
                    player.PlayerComponent.JumpVelocity -= velocityDecrementStep;
                    verticalMovement = 1f;
                }
                else
                {
                    // If jumpvelocity is low enough start moving down again when unsupported
                    if (supportState == PlayerSupportState.Unsupported)
                        verticalMovement = -1f;
                } 
                
                float2 input = player.Input.Movement;
                float3 wantedMove = new float3(input.x, verticalMovement, input.y);
                
                wantedMove = math.normalizesafe(wantedMove) * movementSpeed;
                
                // Wanted movement is relative to camera
                wantedMove = math.rotate(quaternion.RotateY(player.Input.Yaw), wantedMove);
                // Keep track of rotations
                player.PlayerComponent.Pitch = player.Input.Pitch;
                player.PlayerComponent.Yaw = player.Input.Yaw;
                
                MovePlayerCheckCollisions(SystemAPI.Time.DeltaTime, ref pos, ref wantedMove, player.ContainingArea.Area, containingAreaLoc, supportState, supportedY);
                
                player.Transform.ValueRW.Position = pos;
            }*/
        }


        [BurstCompile]
        [WithAll(typeof(Simulate), typeof(PlayerInGame))]
        public partial struct MovePlayerJob : IJobEntity
        {
            // Terrain structure references
            [ReadOnly]public BufferLookup<TerrainBlocks> terrainBlockLookup;
            [ReadOnly]public ComponentLookup<TerrainNeighbors> terrainNeighborLookup;
            [ReadOnly]public NativeArray<Entity> terrainAreasEntities;
            [ReadOnly]public NativeArray<TerrainArea> terrainAreas;
        
            // Static offsets defining player size when used for collision and checking ground support
            [ReadOnly]public NativeHashSet<float3> playerSupportOffsets;
            [ReadOnly]public NativeHashSet<float3> playerCollisionOffsets;
        
            // World generation information
            [ReadOnly]public int columnHeight;
            
            // Movement variables
            [ReadOnly]public int velocityDecrementStep;
            [ReadOnly]public float movementSpeed;
            
            public void Execute(Entity entity,
                in AutoCommandTarget autoCommandTarget,
                ref PlayerComponent playerComponent,
                ref PlayerContainingArea playerContainingArea,
                in PlayerInput playerInput,
                ref LocalTransform playerTransform)
            {
                if (!autoCommandTarget.Enabled)
                {
                    return;
                }
                
                // Reusable block search input/output structs
                TerrainUtilities.BlockSearchInput BSI = default;
                TerrainUtilities.BlockSearchInput.DefaultBlockSearchInput(ref BSI);
                TerrainUtilities.BlockSearchOutput BSO = default;
                TerrainUtilities.BlockSearchOutput.DefaultBlockSearchOutput(ref BSO);
                float3 pos = playerTransform.Position;
                
                
                // Check the terrain areas underneath the player
                int containingAreaIndex = GetPlayerContainingArea(in pos, in terrainAreas, out int3 containingAreaLoc);
                PlayerSupportState supportState = PlayerSupportState.Unsupported;
                int supportedY = -1;
                if (containingAreaIndex != -1)
                {
                    playerContainingArea.Area = terrainAreasEntities[containingAreaIndex];
                    playerContainingArea.AreaLocation = containingAreaLoc;
                    supportState = CheckPlayerSupported(ref BSI, ref BSO, in playerContainingArea.Area, in containingAreaLoc,
                        columnHeight, in playerSupportOffsets, in playerTransform.Position, in terrainBlockLookup,
                        in terrainNeighborLookup, ref supportedY);
                }
                else
                {
                    playerContainingArea.Area = Entity.Null;
                    playerContainingArea.AreaLocation = new int3(-1);
                }
                

                // Simple jump mechanism, when jump event is set the jump velocity is set
                // then on each tick it is decremented. It results in an input value being set either
                // in the upward or downward direction (just like left/right movement).
                if (supportState == PlayerSupportState.Supported && playerInput.Jump.IsSet)
                {
                    // Allow jump and stop falling when grounded
                    playerComponent.JumpVelocity = 20;
                }
                
                var verticalMovement = 0f;
                if (playerComponent.JumpVelocity > 0)
                {
                    playerComponent.JumpVelocity -= velocityDecrementStep;
                    verticalMovement = 1f;
                }
                else
                {
                    // If jumpvelocity is low enough start moving down again when unsupported
                    if (supportState == PlayerSupportState.Unsupported)
                        verticalMovement = -1f;
                } 
                
                float2 input = playerInput.Movement;
                float3 wantedMove = new float3(input.x, verticalMovement, input.y);
                
                wantedMove = math.normalizesafe(wantedMove) * movementSpeed;
                
                // Wanted movement is relative to camera
                wantedMove = math.rotate(quaternion.RotateY(playerInput.Yaw), wantedMove);
                // Keep track of rotations
                playerComponent.Pitch = playerInput.Pitch;
                playerComponent.Yaw = playerInput.Yaw;
                
                
                
                MovePlayerCheckCollisions(ref BSI, ref BSO, ref pos, ref wantedMove,
                    in playerContainingArea.Area, in containingAreaLoc, columnHeight, supportState, supportedY,
                    in playerCollisionOffsets, in terrainBlockLookup, in terrainNeighborLookup);

                playerTransform.Position = pos;
            }
        }


        [BurstCompile]
        // Checks if the blocks under a player exist in the terrain
        private static int GetPlayerContainingArea(in float3 pos,in NativeArray<TerrainArea> terrainAreas, out int3 containingAreaLoc)
        {
            var playerAreaLoc = TerrainUtilities.GetContainingAreaLocation(in pos);
            
            if (!TerrainUtilities.GetTerrainAreaByPosition(in playerAreaLoc, terrainAreas, out int containingAreaIndex))
            {
                containingAreaLoc = new int3(-1);
                return -1;
            }

            containingAreaLoc = playerAreaLoc;
            return containingAreaIndex;
            
        }

        [BurstCompile]
        // Checks if the blocks under a player exist in the terrain
        private static PlayerSupportState CheckPlayerSupported(ref TerrainUtilities.BlockSearchInput BSI, ref TerrainUtilities.BlockSearchOutput BSO, in Entity containingArea,
            in int3 containingAreaLoc, int columnHeight, in NativeHashSet<float3> playerSupportOffsets, in float3 pos, in BufferLookup<TerrainBlocks> terrainBlockLookup,
            in ComponentLookup<TerrainNeighbors> terrainNeighborLookup, ref int supportedY)
        {
            // Setup search inputs
            TerrainUtilities.BlockSearchInput.DefaultBlockSearchInput(ref BSI);
            BSI.offset = int3.zero;
            BSI.areaEntity = containingArea;
            BSI.terrainAreaPos = containingAreaLoc;
            BSI.columnHeight = columnHeight;
            
            // Check corners under player
            foreach (var offset in playerSupportOffsets)
            {
                TerrainUtilities.BlockSearchOutput.DefaultBlockSearchOutput(ref BSO);
                BSI.basePos = NoiseUtilities.FastFloor(pos + offset);
                
                if (TerrainUtilities.GetBlockAtPositionByOffset(in BSI, ref BSO,
                        in terrainNeighborLookup, in terrainBlockLookup))
                {
                    if (BSO.blockType != BlockType.Air)
                    {
                        supportedY = BSI.basePos.y;
                        return PlayerSupportState.Supported;
                    }
                }

            }
            return PlayerSupportState.Unsupported;

        }


        [BurstCompile]
        // Checks if there are blocks in the way of the player's movement
        private static void MovePlayerCheckCollisions(ref TerrainUtilities.BlockSearchInput BSI, ref TerrainUtilities.BlockSearchOutput BSO, ref float3 pos, ref float3 wantedMove,
            in Entity containingArea, in int3 containingAreaLoc, int columnHeight, PlayerSupportState supportState, int supportY, in NativeHashSet<float3> playerCollisionOffsets,
            in BufferLookup<TerrainBlocks> terrainBlockLookup, in ComponentLookup<TerrainNeighbors> terrainNeighborLookup)
        {
            float3 newPosition = pos + wantedMove;
            // Prevent falling out of bounds
            if (newPosition.y < 1.0f)
                newPosition.y = 1.0f;
            
            // Prevent clipping into supporting block when tick rate is low
            if (supportState == PlayerSupportState.Supported && newPosition.y < supportY+2)
            {
                //Debug.Log($"Prevented clip into y={supportY}");
                newPosition.y = supportY + 2;
            }
            
            
            // Setup search inputs
            TerrainUtilities.BlockSearchInput.DefaultBlockSearchInput(ref BSI);
            BSI.offset = int3.zero;
            BSI.areaEntity = containingArea;
            BSI.terrainAreaPos = containingAreaLoc;
            BSI.columnHeight = columnHeight;

            if (containingArea == Entity.Null)
            {
                pos = newPosition;
                return;
            }
            
            foreach (var offset in playerCollisionOffsets)
            {
                TerrainUtilities.BlockSearchOutput.DefaultBlockSearchOutput(ref BSO);
                BSI.basePos = NoiseUtilities.FastFloor(newPosition + offset);

                if (TerrainUtilities.GetBlockAtPositionByOffset(in BSI, ref BSO,
                        in terrainNeighborLookup, in terrainBlockLookup))
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





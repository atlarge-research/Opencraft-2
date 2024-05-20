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

        // World generation information
        private int _columnHeight;

        // Reusable block search input/output structs
        private TerrainUtilities.BlockSearchInput BSI;
        private TerrainUtilities.BlockSearchOutput BSO;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {

            state.RequireForUpdate<PlayerComponent>();
            state.RequireForUpdate<TerrainArea>();
            state.RequireForUpdate<TerrainSpawner>();
            state.RequireForUpdate<WorldParameters>();
            _terrainBlockLookup = state.GetBufferLookup<TerrainBlocks>(true);
            _terrainNeighborLookup = state.GetComponentLookup<TerrainNeighbors>(true);

            _columnHeight = -1;

            TerrainUtilities.BlockSearchInput.DefaultBlockSearchInput(ref BSI);
            TerrainUtilities.BlockSearchOutput.DefaultBlockSearchOutput(ref BSO);
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

            _terrainBlockLookup.Update(ref state);
            _terrainNeighborLookup.Update(ref state);

            new SetSelectedBlock()
            {
                columnHeight = _columnHeight,
                raycastLength = raycastLength,
                camOffset = camOffset,
                terrainNeighborsLookup = _terrainNeighborLookup,
                terrainBlockLookup = _terrainBlockLookup
            }.ScheduleParallel();
        }
    }


    [BurstCompile]
    [WithAll(typeof(Simulate), typeof(PlayerInGame))]
    public partial struct SetSelectedBlock : IJobEntity
    {
        public int columnHeight;

        public int raycastLength;

        public float3 camOffset;

        [ReadOnly][NativeDisableParallelForRestriction] public ComponentLookup<TerrainNeighbors> terrainNeighborsLookup;
        [ReadOnly][NativeDisableParallelForRestriction] public BufferLookup<TerrainBlocks> terrainBlockLookup;

        // Reusable block search input/output structs
        private TerrainUtilities.BlockSearchInput BSI;
        private TerrainUtilities.BlockSearchOutput BSO;

        public void Execute(Entity entity,
            ref SelectedBlock selectedBlock,
            in PlayerContainingArea playerContainingArea,
            in PlayerInput playerInput,
            in LocalTransform playerTransform)
        {
            selectedBlock.blockLoc = new int3(-1);
            selectedBlock.terrainArea = Entity.Null;
            selectedBlock.neighborBlockLoc = new int3(-1);
            selectedBlock.neighborTerrainArea = Entity.Null;

            if (playerContainingArea.Area == Entity.Null)
                return;

            // Use player input Yaw/Pitch to calculate the camera direction on clients
            var cameraRot = math.mul(quaternion.RotateY(playerInput.Yaw),
                quaternion.RotateX(-playerInput.Pitch));
            var direction = math.mul(cameraRot, math.forward());
            Entity neighborTerrainArea = Entity.Null;
            int3 neighborBlockLoc = new int3(-1);


            // Setup search inputs
            TerrainUtilities.BlockSearchInput.DefaultBlockSearchInput(ref BSI);
            BSI.basePos = NoiseUtilities.FastFloor(playerTransform.Position);
            BSI.offset = int3.zero;
            BSI.areaEntity = playerContainingArea.Area;
            BSI.terrainAreaPos = playerContainingArea.AreaLocation;
            BSI.columnHeight = columnHeight;

            // Step along a ray from the players position in the direction their camera is looking
            for (int i = 0; i < raycastLength; i++)
            {
                //float3 location = cameraPos + (direction * i);
                TerrainUtilities.BlockSearchOutput.DefaultBlockSearchOutput(ref BSO);
                BSI.basePos = NoiseUtilities.FastFloor(playerTransform.Position + camOffset + (direction * i));
                if (TerrainUtilities.GetBlockAtPositionByOffset(in BSI, ref BSO,
                        in terrainNeighborsLookup, in terrainBlockLookup))
                {
                    if (BSO.blockType != BlockType.Air)
                    {
                        // found selected block
                        selectedBlock.blockLoc = BSO.localPos;
                        selectedBlock.terrainArea = BSO.containingArea;
                        // Set neighbor
                        selectedBlock.neighborBlockLoc = neighborBlockLoc;
                        selectedBlock.neighborTerrainArea = neighborTerrainArea;

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
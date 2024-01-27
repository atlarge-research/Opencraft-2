using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Opencraft.Player
{
    /// <summary>
    /// Determine a suitable spawn location for players. Searches down for a y value from the max height in x,z = 0,0
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct SetPlayerSpawnLocationSystem : ISystem
    {
        private BufferLookup<TerrainBlocks> _terrainBlockLookup;
        private ComponentLookup<TerrainNeighbors> _terrainNeighborLookup;
        private EntityQuery _terrainAreasQuery;
        // Reusable block search input/output structs
        private TerrainUtilities.BlockSearchInput BSI;
        private TerrainUtilities.BlockSearchOutput BSO;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainArea>();
            state.RequireForUpdate<WorldParameters>();
            _terrainBlockLookup = state.GetBufferLookup<TerrainBlocks>(true);
            _terrainNeighborLookup = state.GetComponentLookup<TerrainNeighbors>(true);
            _terrainAreasQuery= SystemAPI.QueryBuilder().WithAll<TerrainArea, LocalTransform>().Build();
            TerrainUtilities.BlockSearchInput.DefaultBlockSearchInput(ref BSI);
            TerrainUtilities.BlockSearchOutput.DefaultBlockSearchOutput(ref BSO);
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _terrainBlockLookup.Update(ref state);
            _terrainNeighborLookup.Update(ref state);
            
            var worldParameters = SystemAPI.GetSingleton<WorldParameters>();
            Entity entity = SystemAPI.GetSingletonEntity<WorldParameters>();
            int worldHeight = worldParameters.ColumnHeight * Env.AREA_SIZE;
            
            // Find a suitable player spawn at x,z = 0,0
            
            NativeArray<TerrainArea> terrainAreas = _terrainAreasQuery.ToComponentDataArray<TerrainArea>(state.WorldUpdateAllocator);
            NativeArray<Entity> terrainAreasEntities = _terrainAreasQuery.ToEntityArray(state.WorldUpdateAllocator);
            
            int3 areaPos = new int3(0,worldHeight - Env.AREA_SIZE,0);
            if (!TerrainUtilities.GetTerrainAreaByPosition(in areaPos,
                    in terrainAreas,
                    out int containingAreaIndex))
            {
                // Try again next tick
                //Debug.Log($"SetPlayerSpawnLocationSystem: Can't find {areaPos} area");
                
                return;
            }

            Entity terrainEntity = terrainAreasEntities[containingAreaIndex];
            
            // Setup search inputs
            TerrainUtilities.BlockSearchInput.DefaultBlockSearchInput(ref BSI);
            BSI.basePos = new int3(0,worldHeight-1,0);
            BSI.areaEntity = terrainEntity;
            BSI.terrainAreaPos = areaPos ;
            BSI.columnHeight = worldParameters.ColumnHeight;
            
            int yVal = worldHeight - 1;
            for (int i = 1; i < worldHeight; i++) {
                TerrainUtilities.BlockSearchOutput.DefaultBlockSearchOutput(ref BSO);
                
                BSI.offset = new int3(0, -i, 0);
                
                if (TerrainUtilities.GetBlockAtPositionByOffset(in BSI, ref BSO, in _terrainNeighborLookup, in _terrainBlockLookup)) {
                    if (BSO.blockType != BlockType.Air) {
                        yVal = worldHeight - i + 1; // offset by +2 so player isn't stuck in a floor
                        break;
                    }
                    
                }
                else
                {
                    if (BSO.result == TerrainUtilities.BlockSearchResult.NotLoaded)
                    {
                        // Try again next tick
                        //Debug.Log($"SetPlayerSpawnLocationSystem: Found unloaded area...");
                        return;
                    }
                }
            }
            Debug.Log($"Setting player spawn to 0, {yVal}, 0");
            state.EntityManager.AddComponentData(entity, new PlayerSpawn { location = new float3(0.5f, yVal + 0.5f, 0.5f) });
            state.Enabled = false;
        }
    }
}
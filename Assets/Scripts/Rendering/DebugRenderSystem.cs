using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Utilities;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Opencraft.Rendering
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation, WorldSystemFilterFlags.Editor)]
    [BurstCompile]
    // Draws bounding boxing on terrain area borders, only in the editor.
    public partial struct DebugRenderSystem : ISystem
    {
        private EntityQuery _terrainSpawnerQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainSpawner>();
            state.RequireForUpdate<TerrainArea>();
            _terrainSpawnerQuery = state.GetEntityQuery(ComponentType.ReadOnly<TerrainSpawner>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TerrainSpawner terrainSpawner = _terrainSpawnerQuery.GetSingleton<TerrainSpawner>();

            new DebugDrawTerrain { blocksPerSide = terrainSpawner.blocksPerSide }.ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct DebugDrawTerrain : IJobEntity
    {
        public int blocksPerSide;

        public void Execute(in TerrainArea terrainChunk, in LocalTransform t)
        {

            var baseLocation = t.Position;
            TerrainUtilities.DebugDrawTerrainArea(ref baseLocation, Color.white);
        }
    }
}


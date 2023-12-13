using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Utilities;
using Unity.Burst;
using Unity.Entities;
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

        //[BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainArea>();
            if (!PolkaDOTS.ApplicationConfig.DebugEnabled)
            {
                state.Enabled = false;
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
           new DebugDrawTerrain().ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct DebugDrawTerrain : IJobEntity
    {

        public void Execute(in TerrainArea terrainChunk, in LocalTransform t)
        {

            var baseLocation = t.Position;
            TerrainUtilities.DebugDrawTerrainArea(in baseLocation, Color.white);
        }
    }
}


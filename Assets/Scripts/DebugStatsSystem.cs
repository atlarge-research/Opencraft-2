using Opencraft.Terrain;
using Unity.Entities;

namespace Opencraft
{
    // Populates the debug statistics static class
    public partial struct DebugStatsSystem : ISystem
    {
        private EntityQuery terrainAreaQuery;

        public void OnCreate(ref SystemState state)
        {
            terrainAreaQuery = state.EntityManager.CreateEntityQuery(typeof(TerrainArea));
        }

        public void OnUpdate(ref SystemState state)
        {
            DebugStats.numAreas = terrainAreaQuery.CalculateEntityCount();
        }
    }
}
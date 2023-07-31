using Opencraft.Terrain.Authoring;
using Unity.Entities;

namespace Opencraft.Statistics
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct StatisticsSystemServer : ISystem
    {
        private EntityQuery _terrainAreaQuery;
        
        public void OnCreate(ref SystemState state)
        {
            _terrainAreaQuery = state.EntityManager.CreateEntityQuery(typeof(TerrainArea));
        }
        
        public void OnUpdate(ref SystemState state)
        {
            GameStatistics.NumTerrainAreasServer.Value = _terrainAreaQuery.CalculateEntityCount();
        }
    }
    
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct StatisticsSystemClient : ISystem
    {
        private EntityQuery _terrainAreaQuery;
        
        public void OnCreate(ref SystemState state)
        {
            _terrainAreaQuery = state.EntityManager.CreateEntityQuery(typeof(TerrainArea));
        }
        
        public void OnUpdate(ref SystemState state)
        {
            GameStatistics.NumTerrainAreasClient.Value = _terrainAreaQuery.CalculateEntityCount();
        }
    }
}
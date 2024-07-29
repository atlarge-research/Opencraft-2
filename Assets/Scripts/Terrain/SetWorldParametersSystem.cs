using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Layers;
using Unity.Collections;
using Unity.Entities;
using Unity.VisualScripting;

namespace Opencraft.Terrain
{
    /// <summary>
    /// Create a <see cref="WorldParameters"/> singleton to hold world generation information
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TerrainToSpawn))]
    public partial struct SetWorldParametersSystem : ISystem
    {
        private FixedString128Bytes _worldConfigName;
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainSpawner>();
            _worldConfigName = new FixedString128Bytes(GameConfig.TerrainType.Value);
        }

        public void OnUpdate(ref SystemState state)
        {
            TerrainBank terrain = TerrainBankSingleton.Instance;
            if (terrain.IsUnityNull())
                return;

            TerrainGenerationConfiguration config = terrain.terrainConfigBank.Configs[0];
            if (_worldConfigName != "default")
                foreach (var terrainConfig in terrain.terrainConfigBank.Configs)
                    if (terrainConfig.Name == _worldConfigName)
                        config = terrainConfig;

            TerrainSpawner terrainSpawner = SystemAPI.GetSingleton<TerrainSpawner>();
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            Entity terrainConfigEntity = ecb.Instantiate(terrainSpawner.TerrainConfiguration);
            ecb.SetComponent(terrainConfigEntity, new WorldParameters { ColumnHeight = config.AreaColumnHeight });

            foreach (var layer in config.Layers)
            {
                Entity layerEntity = ecb.CreateEntity();

                ecb.AddComponent(layerEntity, new TerrainGenerationLayer
                {
                    layerType = layer.LayerType,
                    index = layer.Index,
                    blockType = layer.BlockType,
                    structureType = layer.StructureType,
                    frequency = layer.Frequency,
                    exponent = layer.Exponent,
                    //baseHeight = layer.BaseHeight,
                    minHeight = layer.MinHeight,
                    maxHeight = layer.MaxHeight,
                    amplitude = layer.MaxHeight - layer.MinHeight,
                    chance = layer.Chance,
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            state.Enabled = false;
        }

    }
}
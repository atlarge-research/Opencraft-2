using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Layers;
using Opencraft.Terrain.Structures;
using Opencraft.Terrain.Utilities;
using Unity.Entities;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

namespace Opencraft.Terrain.Authoring
{
    [InternalBufferCapacity(64)]
    // Buffer of terrain area columns we need to spawn but haven't yet
    public struct TerrainColumnsToSpawn : IBufferElementData, ISingleton
    {
        public int2 ColumnPos;
    }
    
    
    
    
    // Singleton holding the TerrainArea prefab entity we instantiate, and some world settings
    public struct TerrainSpawner : IComponentData, ISingleton
    {
        public Entity TerrainArea;
        public int seed;
        public int maxColumnSpawnsPerTick;
        public int2 worldLimitsHeight;
        public int playerViewRange;
        public int terrainSpawnRange;
    }

    // Manage component of materials used on terrain area meshes
    public class MaterialBank : IComponentData, ISingleton
    {
        public Material TerrainMaterial;
    }
    
    public struct TerrainGenerationLayer : IComponentData
    {
        public LayerType layerType;
        public int index;
        public BlockType blockType;
        public StructureType structureType;
        public float frequency;
        public float exponent;
        //public int baseHeight;
        public int minHeight;
        public int maxHeight;
        public int amplitude;
        public float chance;
    }

    [DisallowMultipleComponent]
    public class TerrainSpawnerAuthoring : MonoBehaviour
    {
        public GameObject TerrainArea;
        public Material TerrainMaterial;
        public float[] TerrainMaterialUVSizing= new float[] { 1.0f,1.0f, 1.0f,1.0f, 1.0f,1.0f, 1.0f,1.0f,1.0f,1.0f};
        public LayerCollection layerCollection = null;
        public int seed = 42;
        public int initialColumnsX = 3;
        public int initialColumnsZ = 3;
        public int playerViewRange = 5;
        public int terrainSpawnRange = 5;
        public int maxColumnSpawnsPerTick = 10;

        class Baker : Baker<TerrainSpawnerAuthoring>
        {
            public override void Bake(TerrainSpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                // Create a TerrainSpawner component and fill its fields
                TerrainSpawner terrainSpawner = new TerrainSpawner
                {
                    TerrainArea = GetEntity(authoring.TerrainArea, TransformUsageFlags.Dynamic),
                    maxColumnSpawnsPerTick = authoring.maxColumnSpawnsPerTick,
                    seed = CmdArgs.seed != -1 ? CmdArgs.seed : authoring.seed,
                    playerViewRange = authoring.playerViewRange,
                    terrainSpawnRange = authoring.terrainSpawnRange
                };
                authoring.TerrainMaterial.SetFloatArray("_uvSizes", authoring.TerrainMaterialUVSizing);
                MaterialBank materialBank = new MaterialBank() { TerrainMaterial = authoring.TerrainMaterial };

                // Add to the TerrainSpawner entity a buffer of terrain areas to spawn
                var columnsToSpawnBuffer = AddBuffer<TerrainColumnsToSpawn>(entity);
                DynamicBuffer<int2> intBuffer = columnsToSpawnBuffer.Reinterpret<int2>();
                for (int x = -(int)math.floor(authoring.initialColumnsX / 2.0f);
                     x < math.ceil(authoring.initialColumnsX / 2.0f);
                     x++)
                    for (int z = -(int)math.floor(authoring.initialColumnsZ / 2.0f);
                         z < math.ceil(authoring.initialColumnsZ / 2.0f);
                         z++)
                        intBuffer.Add(new int2(x, z));

                AddComponent(entity, terrainSpawner);
                AddComponentObject(entity, materialBank);
                
                authoring.layerCollection.SortLayers();
                foreach(var layer in authoring.layerCollection.Layers)
                {
                    Entity layerEntity = CreateAdditionalEntity(TransformUsageFlags.None, entityName:layer.LayerName);
                    AddComponent(layerEntity, new TerrainGenerationLayer
                    {
                        layerType=layer.LayerType,
                        index=layer.Index,
                        blockType=layer.BlockType,
                        structureType = layer.StructureType,
                        frequency = layer.Frequency,
                        exponent =layer.Exponent,
                        //baseHeight = layer.BaseHeight,
                        minHeight = layer.MinHeight,
                        maxHeight= layer.MaxHeight,
                        amplitude = layer.MaxHeight - layer.MinHeight,
                        chance = layer.Chance,
                    });
                }
                
            }
        }
    }
}
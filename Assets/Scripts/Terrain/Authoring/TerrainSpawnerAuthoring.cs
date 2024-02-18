using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Layers;
using Opencraft.Terrain.Structures;
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
        public Entity TerrainConfiguration;
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
        public GameObject TerrainConfiguration;
        public Material TerrainMaterial;
        class Baker : Baker<TerrainSpawnerAuthoring>
        {
            public override void Bake(TerrainSpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                // Create a TerrainSpawner component and fill its fields
                TerrainSpawner terrainSpawner = new TerrainSpawner
                {
                    TerrainArea = GetEntity(authoring.TerrainArea, TransformUsageFlags.Dynamic),
                    TerrainConfiguration= GetEntity(authoring.TerrainConfiguration, TransformUsageFlags.None),
                };
                MaterialBank materialBank = new MaterialBank() { TerrainMaterial = authoring.TerrainMaterial };

                // Add to the TerrainSpawner entity a buffer of terrain areas to spawn
                var columnsToSpawnBuffer = AddBuffer<TerrainColumnsToSpawn>(entity);
                DynamicBuffer<int2> intBuffer = columnsToSpawnBuffer.Reinterpret<int2>();
                for (int x = -(int)math.floor(Env.INITIAL_COLUMNS_X / 2.0f);
                     x < math.ceil(Env.INITIAL_COLUMNS_X / 2.0f);
                     x++)
                    for (int z = -(int)math.floor(Env.INITIAL_COLUMNS_Z / 2.0f);
                         z < math.ceil(Env.INITIAL_COLUMNS_Z / 2.0f);
                         z++)
                        intBuffer.Add(new int2(x, z));

                AddComponent(entity, terrainSpawner);
                AddComponentObject(entity, materialBank);
                
            }
        }
    }
}
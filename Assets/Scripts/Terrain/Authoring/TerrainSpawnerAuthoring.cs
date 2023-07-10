using Opencraft.Terrain.Utilities;
using Unity.Entities;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

namespace Opencraft.Terrain.Authoring
{
    [InternalBufferCapacity(64)]
    // Buffer of terrain areas we need to spawn but haven't yet
    public struct TerrainAreasToSpawn : IBufferElementData, ISingleton
    {
        public int3 Value;
    }
    
    // Singleton holding the TerrainArea prefab entity we instantiate, and some world settings
    public struct TerrainSpawner : IComponentData, ISingleton
    {
        public Entity TerrainArea;
        public int seed;
        public int blocksPerSide;
        public int maxChunkSpawnsPerTick;
        public int2 YBounds;
        public int playerViewRange;
    }

    // Manage component of materials used on terrain area meshes
    public class MaterialBank : IComponentData, ISingleton
    {
        public Material TerrainMaterial;
    }

    [DisallowMultipleComponent]
    public class TerrainSpawnerAuthoring : MonoBehaviour
    {
        public GameObject TerrainArea;
        public Material TerrainMaterial;
        public float[] TerrainMaterialUVSizing= new float[] { 1.0f,1.0f, 1.0f,1.0f, 1.0f,1.0f, 1.0f,1.0f,1.0f,1.0f};
        public int seed = 42;
        public int3 initialAreas = new int3(3, 3, 3);
        public int playerViewRange = 5;
        public int maxChunkSpawnsPerTick = 25;
        public int blocksPerAreaSide = 4;

        [Tooltip("x is sea level, y is sky level")]
        public int2 YBounds;

        class Baker : Baker<TerrainSpawnerAuthoring>
        {
            public override void Bake(TerrainSpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                // Create a TerrainSpawner component and fill its fields
                TerrainSpawner terrainSpawner = new TerrainSpawner
                {
                    TerrainArea = GetEntity(authoring.TerrainArea, TransformUsageFlags.Dynamic),
                    blocksPerSide = authoring.blocksPerAreaSide,
                    YBounds = authoring.YBounds,
                    maxChunkSpawnsPerTick = authoring.maxChunkSpawnsPerTick,
                    seed = CmdArgs.seed != -1 ? CmdArgs.seed : authoring.seed,
                    playerViewRange = authoring.playerViewRange
                };
                authoring.TerrainMaterial.SetFloatArray("_uvSizes", authoring.TerrainMaterialUVSizing);
                MaterialBank materialBank = new MaterialBank() { TerrainMaterial = authoring.TerrainMaterial };

                // Add to the TerrainSpawner entity a buffer of terrain areas to spawn
                var toSpawnBuffer = AddBuffer<TerrainAreasToSpawn>(entity);
                DynamicBuffer<int3> intBuffer = toSpawnBuffer.Reinterpret<int3>();
                for (int x = -(int)math.floor(authoring.initialAreas.x / 2.0f);
                     x < math.ceil(authoring.initialAreas.x / 2.0f);
                     x++)
                    for (int y = -(int)math.floor(authoring.initialAreas.y / 2.0f);
                         y < math.ceil(authoring.initialAreas.y / 2.0f);
                         y++)
                        for (int z = -(int)math.floor(authoring.initialAreas.z / 2.0f);
                             z < math.ceil(authoring.initialAreas.z / 2.0f);
                             z++)
                            intBuffer.Add(new int3(x, y, z));

                AddComponent(entity, terrainSpawner);
                AddComponentObject(entity, materialBank);

                BlocksPerAreaSide.BlocksPerSide.Data = authoring.blocksPerAreaSide;
            }
        }
    }
}
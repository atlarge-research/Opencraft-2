using Unity.Entities;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;


[InternalBufferCapacity(64)]
public struct TerrainAreasToSpawn : IBufferElementData, ISingleton
{
    public int3 Value;
}

public struct TerrainSpawner : IComponentData, ISingleton
{
    public Entity TerrainArea;
    public Entity TerrainFace;
    public int seed;
    public int blocksPerChunkSide;
    public int maxChunkSpawnsPerTick; 
    public int2 YBounds;
    public int3 initialAreas;
}

[DisallowMultipleComponent]
public class TerrainSpawnerAuthoring : MonoBehaviour
{
    public GameObject TerrainArea;
    public GameObject TerrainFace;
    public int seed;
    public int3 initialAreas;
    public int maxChunkSpawnsPerTick;
    public int blocksPerChunkSide;
    [Tooltip("x is sea level, y is sky level")] public int2 YBounds;

    class Baker : Baker<TerrainSpawnerAuthoring>
    {
        public override void Bake(TerrainSpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            // Create a TerrainSpawner component and fill its fields
            TerrainSpawner component = default(TerrainSpawner);
            component.TerrainFace = GetEntity(authoring.TerrainFace, TransformUsageFlags.Dynamic);
            component.TerrainArea = GetEntity(authoring.TerrainArea, TransformUsageFlags.Dynamic);
            component.blocksPerChunkSide = authoring.blocksPerChunkSide;
            component.YBounds = authoring.YBounds;
            component.maxChunkSpawnsPerTick = authoring.maxChunkSpawnsPerTick;
            component.seed = authoring.seed;
            component.initialAreas = authoring.initialAreas;
            
            // Add to the TerrainSpawner entity a buffer of int3s, fill it with initial values
            var toSpawnBuffer = AddBuffer<TerrainAreasToSpawn>(entity);
            DynamicBuffer<int3> intBuffer = toSpawnBuffer.Reinterpret<int3>();
            for (int x = 0; x < authoring.initialAreas.x; x++)
                for (int y = 0; y < authoring.initialAreas.y; y++)
                    for (int z = 0; z < authoring.initialAreas.z; z++)
                        intBuffer.Add(new int3(x, y, z));
            
            AddComponent(entity, component);
            
        }
    }
}
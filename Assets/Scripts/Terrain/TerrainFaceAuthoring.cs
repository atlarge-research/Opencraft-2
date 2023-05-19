using Unity.Entities;
using UnityEngine;

public class TerrainFaceAuthoring : MonoBehaviour { }

public class TerrainFaceBaker : Baker<TerrainFaceAuthoring>
{
    public override void Bake(TerrainFaceAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent<TerrainFace>(entity);
    }
}

public struct TerrainFace : IComponentData { }



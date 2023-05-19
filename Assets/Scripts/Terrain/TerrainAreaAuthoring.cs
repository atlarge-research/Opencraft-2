using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

public class TerrainAreaAuthoring : MonoBehaviour
{
    public class TerrainBlockBaker : Baker<TerrainAreaAuthoring>
    {
        public override void Bake(TerrainAreaAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<TerrainArea>(entity);
            AddComponent<NewSpawn>(entity);
            AddComponent<NotRendered>(entity);
            AddBuffer<TerrainBlocks>(entity);
            //SetComponentEnabled<Visible>(entity, false);
        }
    }
}

public struct TerrainArea: IComponentData
{
    [GhostField] public int3 location;
    [GhostField] public int numBlocks;
}
public struct NewSpawn: IComponentData, IEnableableComponent {}
public struct NotRendered: IComponentData, IEnableableComponent {}

// todo don't store x,y,z, infer it from index
[InternalBufferCapacity(512)]
public struct TerrainBlocks : IBufferElementData
{
    [GhostField] public int4 Value; // x,y,z,type if exists, all -1s if not
}





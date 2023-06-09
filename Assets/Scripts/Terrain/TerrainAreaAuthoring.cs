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
            var notRendered = new NotRendered {};
            AddComponent(entity, notRendered);
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

public struct NotRendered : IComponentData, IEnableableComponent {}


[InternalBufferCapacity(512)]
public struct TerrainBlocks : IBufferElementData
{
    [GhostField] public int Value; //type if exists, -1 if not
}





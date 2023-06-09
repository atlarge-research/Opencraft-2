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
            var terrainArea = new TerrainArea()
            {
                neighborXP = Entity.Null,
                neighborXN = Entity.Null,
                neighborYP = Entity.Null,
                neighborYN = Entity.Null,
                neighborZP = Entity.Null,
                neighborZN = Entity.Null,
            };
            AddComponent(entity, terrainArea);
            AddComponent<NewSpawn>(entity);
            AddBuffer<TerrainBlocks>(entity);

        }
    }
}

public struct TerrainArea: IComponentData
{
    [GhostField] public int3 location;
    public Entity neighborXP;
    public Entity neighborXN;
    public Entity neighborYP;
    public Entity neighborYN;
    public Entity neighborZP;
    public Entity neighborZN;
}

// NewSpawn component marks an entity as freshly instantiated.
// todo - when to disable the newspawn component? End of same tick? Currently done at start of next tick by TerrainGenerationSystem on server
// todo - and by end of same tick by TerrainRenderInitSystem on client
public struct NewSpawn: IComponentData, IEnableableComponent {}

// Remesh component marks an entity as needing to be remeshed
public struct Remesh: IComponentData, IEnableableComponent {}

[InternalBufferCapacity(512)]
public struct TerrainBlocks : IBufferElementData
{
    [GhostField] public int Value; //type if exists, -1 if not
}





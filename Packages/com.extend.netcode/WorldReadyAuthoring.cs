using Unity.Entities;
using UnityEngine;

namespace Unity.NetCode
{
    public class WorldReadyAuthoring : MonoBehaviour
    {
        class Baker : Baker<WorldReadyAuthoring>
        {
            public override void Bake(WorldReadyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new WorldReady());
            }
        }
    }
}
using Unity.Entities;
using UnityEngine;

namespace Opencraft.Rendering.Authoring
{
    public struct BlockOutline : IComponentData
    { }
    
    public class BlockOutlineAuthoring : MonoBehaviour
    {
        class Baker : Baker<BlockOutlineAuthoring>
        {
            public override void Bake(BlockOutlineAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new BlockOutline());
            }
        }
    }
}
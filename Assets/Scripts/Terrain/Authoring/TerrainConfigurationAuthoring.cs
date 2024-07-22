using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.VisualScripting;
using UnityEngine;

namespace Opencraft.Terrain.Authoring
{
    public class TerrainConfigurationAuthoring : MonoBehaviour
    {
        public class TerrainConfigBaker : Baker<TerrainConfigurationAuthoring>
        {
            public override void Bake(TerrainConfigurationAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent<WorldParameters>(entity);
            }
        }
    }
    
    [GhostComponent(PrefabType=GhostPrefabType.All)]
    public struct WorldParameters : IComponentData, ISingleton
    {
        [GhostField]public int ColumnHeight;
    }

    public struct PlayerSpawn : IComponentData, ISingleton
    {
        public float3 location;
    }
}
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Unity.Burst;
using Unity.Entities;

namespace Opencraft.Rendering
{
    /// <summary>
    /// Initializes the terrain material, only necessary in worlds running a presentation frontend
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
    [BurstCompile]
    public partial class InitializeMaterialSystem : SystemBase
    {
        private EntityQuery _materialQuery;
        protected override void OnCreate()
        {
            _materialQuery = GetEntityQuery(ComponentType.ReadOnly<MaterialBank>());
            RequireForUpdate(_materialQuery);
        }
        
        [BurstCompile]
        protected override void OnUpdate()
        {
            MaterialBank materialBank = _materialQuery.GetSingleton<MaterialBank>();
            materialBank.TerrainMaterial.SetFloatArray("_uvSizes", BlockData.BlockUVSizing);
            Enabled = false;
        }
        
    }
}
using Opencraft.Terrain.Authoring;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Opencraft.Rendering
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
    [BurstCompile]
    public partial class TerrainChangeMonitoringSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<TerrainSpawner>();
            RequireForUpdate<TerrainArea>();
        }
        
        [BurstCompile]
        protected override void OnUpdate()
        {
            Entities
                .WithChangeFilter<TerrainBlocks>()
                .WithImmediatePlayback()
                .ForEach((Entity entity, EntityCommandBuffer ecb, ref TerrainArea terrainArea) =>
                {
                    // todo- currently we remesh all 6 neighbor areas regardless of where the change was
                    // todo- worst case, we should only need 3 neighbors remeshed, depending on where the change is
                    ecb.SetComponentEnabled<Remesh>(entity, true);
                    if(terrainArea.neighborXN!= Entity.Null)
                        ecb.SetComponentEnabled<Remesh>(terrainArea.neighborXN,true);
                    if(terrainArea.neighborXP!= Entity.Null)
                        ecb.SetComponentEnabled<Remesh>(terrainArea.neighborXP,true);
                    if(terrainArea.neighborYN!= Entity.Null)
                        ecb.SetComponentEnabled<Remesh>(terrainArea.neighborYN,true);
                    if(terrainArea.neighborYP!= Entity.Null)
                        ecb.SetComponentEnabled<Remesh>(terrainArea.neighborYP,true);
                    if(terrainArea.neighborZN!= Entity.Null)
                        ecb.SetComponentEnabled<Remesh>(terrainArea.neighborZN,true);
                    if(terrainArea.neighborZP!= Entity.Null)
                        ecb.SetComponentEnabled<Remesh>(terrainArea.neighborZP,true);
                }).Run();
        }
        
    }
}
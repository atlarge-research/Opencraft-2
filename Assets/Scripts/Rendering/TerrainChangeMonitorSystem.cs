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
                .WithChangeFilter<TerrainBlocks>().WithChangeFilter<BlockPowered>()
                .WithImmediatePlayback()
                .ForEach((Entity entity, EntityCommandBuffer ecb, in TerrainNeighbors terrainNeighbors) =>
                {
                    // todo- currently we remesh all 6 neighbor areas regardless of where the change was
                    // todo- worst case, we should only need 3 neighbors remeshed, depending on where the change is
                    ecb.SetComponentEnabled<Remesh>(entity, true);
                    if (terrainNeighbors.neighborXN != Entity.Null)
                        ecb.SetComponentEnabled<Remesh>(terrainNeighbors.neighborXN, true);
                    if (terrainNeighbors.neighborXP != Entity.Null)
                        ecb.SetComponentEnabled<Remesh>(terrainNeighbors.neighborXP, true);
                    if (terrainNeighbors.neighborYN != Entity.Null)
                        ecb.SetComponentEnabled<Remesh>(terrainNeighbors.neighborYN, true);
                    if (terrainNeighbors.neighborYP != Entity.Null)
                        ecb.SetComponentEnabled<Remesh>(terrainNeighbors.neighborYP, true);
                    if (terrainNeighbors.neighborZN != Entity.Null)
                        ecb.SetComponentEnabled<Remesh>(terrainNeighbors.neighborZN, true);
                    if (terrainNeighbors.neighborZP != Entity.Null)
                        ecb.SetComponentEnabled<Remesh>(terrainNeighbors.neighborZP, true);
                }).Run();
        }

    }
}
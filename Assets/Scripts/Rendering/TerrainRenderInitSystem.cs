using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Rendering;
using UnityEngine;

// Adds components to TerrainArea entities on client side for rendering purposes
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[UpdateAfter(typeof(TerrainNeighborSystem))]
public partial class TerrainRenderInitSystem : SystemBase
{
    private EntityQuery _terrainSpawnerQuery;
    private EntityQuery _newSpawnQuery;
    protected override void OnCreate()
    {
        RequireForUpdate<TerrainArea>();
        RequireForUpdate<NewSpawn>();
        _terrainSpawnerQuery = GetEntityQuery(ComponentType.ReadOnly<TerrainSpawner>(), ComponentType.ReadOnly<MaterialBank>());
        _newSpawnQuery = GetEntityQuery(ComponentType.ReadOnly<TerrainArea>(), ComponentType.ReadOnly<NewSpawn>());
    }
    
    protected override void OnUpdate()
    {
        if (_newSpawnQuery.IsEmpty)
            return;
        var terrainSpawner = _terrainSpawnerQuery.GetSingleton<TerrainSpawner>();
        // On each new terrain area, add a new Mesh managed object
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        float3 boundsExtents = new float3(0.75 * terrainSpawner.blocksPerSide);
        MaterialBank materialBank = _terrainSpawnerQuery.GetSingleton<MaterialBank>();
        foreach (var ( terrainArea, entity) in SystemAPI.Query< RefRO<TerrainArea>>().WithAll<NewSpawn>().WithEntityAccess())
        {
            int3 loc = terrainArea.ValueRO.location;
            Mesh mesh = new Mesh{name=$"mesh_{loc.x},{loc.y},{loc.z}"};

            RenderMeshArray renderMeshArray = new RenderMeshArray(new []{materialBank.material1}, new []{mesh});
            var materialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0);

            ecb.AddSharedComponentManaged(entity, renderMeshArray);
            ecb.AddComponent(entity, materialMeshInfo);
            ecb.AddComponent(entity, new RenderBounds{Value = new AABB(){Center = new float3(0.0f), Extents = boundsExtents}});
            ecb.AddSharedComponentManaged(entity, RenderFilterSettings.Default);
            ecb.AddComponent(entity, new ComponentTypeSet(new []
            {
                ComponentType.ReadWrite<Remesh>(),
                // Entities without these components will not match queries and will never be rendered.
                ComponentType.ReadWrite<WorldRenderBounds>(),
                ComponentType.ChunkComponent<ChunkWorldRenderBounds>(),
                ComponentType.ChunkComponent<EntitiesGraphicsChunkInfo>(),
                ComponentType.ReadWrite<WorldToLocal_Tag>(),
                ComponentType.ReadWrite<PerInstanceCullingTag>(),
                
            }));
            
            // Remesh neighbors of the new area to eliminate shared faces
            if (EntityManager.HasComponent<Remesh>(terrainArea.ValueRO.neighborXN))
                EntityManager.SetComponentEnabled<Remesh>(terrainArea.ValueRO.neighborXN, true);
            if(EntityManager.HasComponent<Remesh>(terrainArea.ValueRO.neighborXP))
                EntityManager.SetComponentEnabled<Remesh>(terrainArea.ValueRO.neighborXP, true);
            if(EntityManager.HasComponent<Remesh>(terrainArea.ValueRO.neighborYN))
                EntityManager.SetComponentEnabled<Remesh>(terrainArea.ValueRO.neighborYN, true);
            if(EntityManager.HasComponent<Remesh>(terrainArea.ValueRO.neighborYP))
                EntityManager.SetComponentEnabled<Remesh>(terrainArea.ValueRO.neighborYP, true);
            if(EntityManager.HasComponent<Remesh>(terrainArea.ValueRO.neighborZN))
                EntityManager.SetComponentEnabled<Remesh>(terrainArea.ValueRO.neighborZN, true);
            if(EntityManager.HasComponent<Remesh>(terrainArea.ValueRO.neighborZP))
                EntityManager.SetComponentEnabled<Remesh>(terrainArea.ValueRO.neighborZP, true);
            
            ecb.SetComponentEnabled<NewSpawn>(entity, false);
        }
        //TODO ECB playback can be deferred to avoid frame stuttering from large structural changes
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}


using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[BurstCompile]
public partial struct DebugRenderSystem : ISystem
{
    private EntityQuery _terrainSpawnerQuery;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TerrainSpawner>();
        state.RequireForUpdate<TerrainArea>();
        _terrainSpawnerQuery = state.GetEntityQuery(ComponentType.ReadOnly<TerrainSpawner>());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        TerrainSpawner terrainSpawner = _terrainSpawnerQuery.GetSingleton<TerrainSpawner>();
        
        new DebugDrawTerrain{blocksPerSide = terrainSpawner.blocksPerSide}.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct DebugDrawTerrain : IJobEntity
{
    public int blocksPerSide;
    public void Execute(in TerrainArea terrainChunk, in LocalTransform t)
    {

        var baseLocation = t.Position;
        var d = blocksPerSide;
        // Draw a bounding box
        Debug.DrawLine(baseLocation, baseLocation + new float3(d,0,0));
        Debug.DrawLine(baseLocation, baseLocation + new float3(0,d,0));
        Debug.DrawLine(baseLocation, baseLocation + new float3(0,0,d));
        Debug.DrawLine(baseLocation + new float3(d,d,0), baseLocation + new float3(d,0,0));
        Debug.DrawLine(baseLocation + new float3(d,d,0), baseLocation + new float3(0,d,0));
        Debug.DrawLine(baseLocation + new float3(d,d,0), baseLocation + new float3(d,d,d));
        Debug.DrawLine(baseLocation + new float3(0,d,d), baseLocation + new float3(0,d,0));
        Debug.DrawLine(baseLocation + new float3(0,d,d), baseLocation + new float3(0,0,d));
        Debug.DrawLine(baseLocation + new float3(0,d,d), baseLocation + new float3(d,d,d));
        Debug.DrawLine(baseLocation + new float3(d,0,d), baseLocation + new float3(d,0,0));
        Debug.DrawLine(baseLocation + new float3(d,0,d), baseLocation + new float3(d,d,d));
        Debug.DrawLine(baseLocation + new float3(d,0,d), baseLocation + new float3(0,0,d));
    }
}


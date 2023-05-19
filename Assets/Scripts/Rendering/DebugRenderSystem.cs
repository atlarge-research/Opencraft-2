using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class DebugRenderSystem : SystemBase
{

    protected override void OnUpdate()
    {
        var terrainSpawner = SystemAPI.GetSingleton<TerrainSpawner>();
        foreach (var terrainArea in SystemAPI.Query<RefRO<TerrainArea>>())
        {
            int d = terrainSpawner.blocksPerChunkSide;
            //terrainArea.ValueRO.location
            float3 baseLocation = new float3(
                terrainArea.ValueRO.location.x,
                terrainArea.ValueRO.location.y,
                terrainArea.ValueRO.location.z);
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
}


using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

[BurstCompile]
[RequireMatchingQueriesForUpdate]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
//[UpdateAfter(typeof(PlayerMovementSystem))]
public partial struct ConnectionPositionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        var grid = state.EntityManager.CreateEntity();
        var m_ScaleFunctionPointer = GhostDistanceImportance.ScaleFunctionPointer;
        state.EntityManager.SetName(grid, "GhostImportanceSingleton");
        state.EntityManager.AddComponentData(grid, new GhostDistanceData
        {
            TileSize = new int3(5, 5, 5),
            TileCenter = new int3(0, 0, 0),
            TileBorderWidth = new float3(1f, 1f, 1f),
        });
        state.EntityManager.AddComponentData(grid, new GhostImportance
        {
            ScaleImportanceFunction = m_ScaleFunctionPointer,
            GhostConnectionComponentType = ComponentType.ReadOnly<GhostConnectionPosition>(),
            GhostImportanceDataType = ComponentType.ReadOnly<GhostDistanceData>(),
            GhostImportancePerChunkDataType = ComponentType.ReadOnly<GhostDistancePartitionShared>(),
        });
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (nID, ghostConnPos) in SystemAPI.Query<NetworkId, RefRW<GhostConnectionPosition>>())
        {
            foreach (var player in SystemAPI.Query<PlayerAspect>().WithAll<Simulate>())
            {
                if (player.OwnerNetworkId == nID.Value)
                {
                    ghostConnPos.ValueRW.Position = player.Transform.ValueRO.Position;
                    break;
                }
            }
        }
    }
}

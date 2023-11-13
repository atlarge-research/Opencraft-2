using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace PolkaDOTS.Networking
{
    [BurstCompile]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    // Sync GhostConnectionPosition on the server to associated player's position
    // todo: update this for Multiplay, i.e. to support are of interest per player, rather than per connection
    public partial struct ConnectionPositionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Construct an entity to act as an importance grid
            var grid = state.EntityManager.CreateEntity();
            var mScaleFunctionPointer = GhostDistanceImportance.ScaleFunctionPointer;
            state.EntityManager.SetName(grid, "GhostImportanceSingleton");
            state.EntityManager.AddComponentData(grid, new GhostDistanceData
            {
                TileSize = new int3(5, 5, 5),
                TileCenter = new int3(0, 0, 0),
                TileBorderWidth = new float3(1f, 1f, 1f),
            });
            state.EntityManager.AddComponentData(grid, new GhostImportance
            {
                ScaleImportanceFunction = mScaleFunctionPointer,
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
                foreach (var (owner, playerLocation) in SystemAPI.Query<RefRO<GhostOwner>, RefRO<LocalTransform>>().WithAll<Player, Simulate>())
                {
                    if (owner.ValueRO.NetworkId == nID.Value)
                    {
                        ghostConnPos.ValueRW.Position = playerLocation.ValueRO.Position;
                        break;
                    }
                }
            }
        }
    }
}

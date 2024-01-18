using PolkaDOTS;
using PolkaDOTS.Networking;
using Unity.Entities;
using Unity.NetCode;
using Unity.Burst;


namespace Opencraft.Player
{
    /// <summary>
    /// System that detects terminated connections, and marks any players being controlled by that connection as no
    /// longer in game.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateBefore(typeof(ConnectionMonitorSystem))]
    public partial class HandleDisconnectSystem : SystemBase
    {
        private BeginSimulationEntityCommandBufferSystem m_CommandBufferSystem;

        protected override void OnCreate()
        {
            // Use an entity command buffer that is resolved at the beginning of the simulation system group
            m_CommandBufferSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
        }
        
        [BurstCompile]
        protected override void OnUpdate()
        {
            var ecb = m_CommandBufferSystem.CreateCommandBuffer();
            foreach (var state in SystemAPI.Query<RefRO<ConnectionState>>().WithNone<NetworkStreamConnection>())
            {
                foreach (var (owner, entity) in SystemAPI.Query<RefRW<GhostOwner>>().WithAll<PlayerInGame, PolkaDOTS.Player>().WithEntityAccess())
                {
                    if (state.ValueRO.NetworkId == owner.ValueRO.NetworkId)
                    {
                        ecb.SetComponentEnabled<PlayerInGame>(entity, false);
                        break;
                    }
                }
            }
            //m_CommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
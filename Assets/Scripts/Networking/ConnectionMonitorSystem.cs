using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Opencraft.Networking
{
    // This component is used to mark connections as initialized to avoid
    // them being processed multiple times.
    public struct InitializedConnection : IComponentData
    {
    }

    // Monitor the state of connections. For now just used for debugging
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial class ConnectionMonitorSystem : SystemBase
    {
        private BeginSimulationEntityCommandBufferSystem m_CommandBufferSystem;

        static readonly FixedString32Bytes[] s_ConnectionState =
        {
            "Unknown",
            "Disconnected",
            "Connecting",
            "Handshake",
            "Connected"
        };

        protected override void OnCreate()
        {
            // Use an entity command buffer that is resolved at the beginning of the simulation system group
            m_CommandBufferSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
        }
        
        [BurstCompile]
        protected override void OnUpdate()
        {
            var now = new FixedString512Bytes(DateTime.Now.TimeOfDay.ToString());
            var ecb = m_CommandBufferSystem.CreateCommandBuffer();
            Entities.WithName("AddConnectionStateToNewConnections").WithNone<ConnectionState>().ForEach((Entity entity,
                in NetworkStreamConnection state) =>
            {
                ecb.AddComponent<ConnectionState>(entity);
            }).Run();

            FixedString32Bytes worldName = World.Name;
            int worldIndex = 0;
            if (int.TryParse(World.Name[World.Name.Length - 1].ToString(), out worldIndex))
                worldIndex++;
            Entities.WithName("InitializeNewConnection").WithNone<InitializedConnection>().ForEach(
                (Entity entity, in NetworkId id) =>
                {
                    ecb.AddComponent(entity, new InitializedConnection());
                    Debug.Log($"[{now}]:[{worldName}] New connection ID:{id.Value}");
                }).Run();

            Entities.WithName("HandleDisconnect").WithNone<NetworkStreamConnection>().ForEach(
                (Entity entity, in ConnectionState state) =>
                {
                    Debug.Log(
                        $"[{now}]:[{worldName}] Connection disconnected ID:{state.NetworkId} Reason:{DisconnectReasonEnumToString.Convert((int)state.DisconnectReason)}");
                    ecb.RemoveComponent<ConnectionState>(entity);
                }).Run();
            m_CommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

// This component is used to mark connections as initialized to avoid
// them being processed multiple times.
public struct InitializedConnection : IComponentData { }

// Monitor the state of connections. For now just used for debugging
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
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
        m_CommandBufferSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var buffer = m_CommandBufferSystem.CreateCommandBuffer();
        Entities.WithName("AddConnectionStateToNewConnections").WithNone<ConnectionState>().ForEach((Entity entity,
            in NetworkStreamConnection state) =>
        {
            buffer.AddComponent<ConnectionState>(entity);
        }).Run();

        FixedString32Bytes worldName = World.Name;
        int worldIndex = 0;
        if (int.TryParse(World.Name[World.Name.Length - 1].ToString(), out worldIndex))
            worldIndex++;
        Entities.WithName("InitializeNewConnection").WithNone<InitializedConnection>().ForEach(
            (Entity entity, in NetworkId id) =>
            {
                buffer.AddComponent(entity, new InitializedConnection());
                Debug.Log($"[{worldName}] New connection ID:{id.Value}");
            }).Run();

        Entities.WithName("HandleDisconnect").WithNone<NetworkStreamConnection>().ForEach(
            (Entity entity, in ConnectionState state) =>
            {
                UnityEngine.Debug.Log(
                    $"[{worldName}] Connection disconnected ID:{state.NetworkId} Reason:{DisconnectReasonEnumToString.Convert((int)state.DisconnectReason)}");
                buffer.RemoveComponent<ConnectionState>(entity);
            }).Run();

        m_CommandBufferSystem.AddJobHandleForProducer(Dependency);
    }
}
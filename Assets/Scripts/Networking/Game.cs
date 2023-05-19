using System;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

// Create a custom bootstrap, which enables auto-connect.
// The bootstrap can also be used to configure other settings as well as to
// manually decide which worlds (client and server) to create based on user input
[UnityEngine.Scripting.Preserve]
public class GameBootstrap : ClientServerBootstrap
{
    public override bool Initialize(string defaultWorldName)
    {
        AutoConnectPort = 7979; // Enabled auto connect
        CreateDefaultClientServerWorlds();
        NetworkStreamReceiveSystem.DriverConstructor = new DriverConstructor();
        return true;
    }
}

public class DriverConstructor : INetworkStreamDriverConstructor
{
    // Custom timeout time
    private static readonly int s_DisconnectTimeout = 2000;

    private NetworkSettings CreateNetworkSettings(int maxFrameTime = 0)
    {
        var settings = new NetworkSettings();
        settings.WithNetworkConfigParameters(
            connectTimeoutMS: 1000,
            disconnectTimeoutMS: s_DisconnectTimeout,
            heartbeatTimeoutMS: s_DisconnectTimeout / 2,
            fixedFrameTimeMS: 0,
            maxFrameTimeMS: maxFrameTime);
        settings.WithReliableStageParameters(windowSize: 32)
            .WithFragmentationStageParameters(payloadCapacity: 16 * 1024);
        return settings;
    }

    public void CreateClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
    {
        var driverInstance = new NetworkDriverStore.NetworkDriverInstance();
#if UNITY_EDITOR || NETCODE_DEBUG
        var settings = CreateNetworkSettings(100);
        driverInstance.simulatorEnabled = NetworkSimulatorSettings.Enabled;
        if (NetworkSimulatorSettings.Enabled)
        {
            NetworkSimulatorSettings.SetSimulatorSettings(ref settings);
            driverInstance.driver = NetworkDriver.Create(new UDPNetworkInterface(), settings);
            DefaultDriverBuilder.CreateClientSimulatorPipelines(ref driverInstance);
        }
        else
        {
            driverInstance.driver = NetworkDriver.Create(new UDPNetworkInterface(), settings);
            DefaultDriverBuilder.CreateClientPipelines(ref driverInstance);
        }
#else
        var settings = CreateNetworkSettings();
        driverInstance.driver = NetworkDriver.Create(new UDPNetworkInterface(), settings);
        DefaultDriverBuilder.CreateClientPipelines(ref driverInstance);
#endif
        driverStore.RegisterDriver(TransportType.Socket, driverInstance);
    }

    public void CreateServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
    {
        var settings = CreateNetworkSettings();
        var driverInstance = new NetworkDriverStore.NetworkDriverInstance();
        driverInstance.driver = NetworkDriver.Create(new UDPNetworkInterface(), settings);
        DefaultDriverBuilder.CreateServerPipelines(ref driverInstance);
        driverStore.RegisterDriver(TransportType.Socket, driverInstance);
    }
}

// Fix for net physics, see https://forum.unity.com/threads/1-0-0-pre-65-short-guide-for-creating-fully-working-builds.1419512/
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
[UpdateInGroup(typeof(InitializationSystemGroup))]
[CreateAfter(typeof(RpcSystem))]
public partial class SetRpcSystemDynamicAssemblyListSystem : SystemBase
{
    protected override void OnCreate()
    {
        SystemAPI.GetSingletonRW<RpcCollection>().ValueRW.DynamicAssemblyList = true;
        Enabled = false;
    }
    protected override void OnUpdate() { }
}


using Opencraft.Player.Authoring;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Networking.Transport;
using Unity.Transforms;
using UnityEngine;

namespace Opencraft.Networking
{
    // RPC request from client to server for game to go "in game" and send game snapshots / inputs
    public struct StartGameStreamRequest : IRpcCommand
    {
    }

    // When client has a connection with network id, tell server to start stream
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct StartGameStreamClientSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerSpawner>();
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<NetworkId>()
                .WithNone<NetworkStreamInGame>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (id, entity) in SystemAPI.Query<RefRO<NetworkId>>().WithEntityAccess()
                         .WithNone<NetworkStreamInGame>())
            {
                commandBuffer.AddComponent<NetworkStreamInGame>(entity);
                var req = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<StartGameStreamRequest>(req);
                Debug.Log("Sending start game stream RPC");
                commandBuffer.AddComponent(req, new SendRpcCommandRequest { TargetConnection = entity });
            }

            commandBuffer.Playback(state.EntityManager);
        }
    }

    // When server receives stream game request, do so and delete the request
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct StartGameStreamServerSystem : ISystem
    {
        private ComponentLookup<NetworkId> networkIdFromEntity;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GhostRelevancy>();
            state.RequireForUpdate<GhostSendSystemData>();
            state.RequireForUpdate<PlayerSpawner>();
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<StartGameStreamRequest>()
                .WithAll<ReceiveRpcCommandRequest>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
            networkIdFromEntity = state.GetComponentLookup<NetworkId>(true);
            
            // Set some networking configuration parameters
            var ghostSendSystemData = SystemAPI.GetSingleton<GhostSendSystemData>();
            ghostSendSystemData.FirstSendImportanceMultiplier = 2;
            
            // Component used on server to tell the ghost synchronization system to ignore certain ghosts
            // for specific connections, specified through GhostRelevancySet
            var ghostRelevancy = SystemAPI.GetSingleton<GhostRelevancy>();
            ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            networkIdFromEntity.Update(ref state);

            foreach (var (reqSrc, reqEntity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
                         .WithAll<StartGameStreamRequest>().WithEntityAccess())
            {
                commandBuffer.AddComponent<NetworkStreamInGame>(reqSrc.ValueRO.SourceConnection);
                // Following line adds a per-connection component that enforces a non-standard packet size for snapshots.
                //commandBuffer.AddComponent(reqSrc.ValueRO.SourceConnection, new NetworkStreamSnapshotTargetSize { Value = NetworkParameterConstants.MTU});
                commandBuffer.AddComponent(reqSrc.ValueRO.SourceConnection,
                    new GhostConnectionPosition { Position = new float3() });
                var networkId = networkIdFromEntity[reqSrc.ValueRO.SourceConnection];

                Debug.Log($"Starting game stream on connection {networkId.Value}!");

                commandBuffer.DestroyEntity(reqEntity);
            }

            commandBuffer.Playback(state.EntityManager);
        }
    }
}

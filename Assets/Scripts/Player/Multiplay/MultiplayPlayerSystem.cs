using System;
using System.Collections;
using System.Collections.Generic;
using Opencraft.Player.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Rendering;
using Unity.RenderStreaming;
using Unity.VisualScripting;
using UnityEngine;

namespace Opencraft.Player.Multiplay
{
    // Run on Multiplay hosts, handles sending player spawn/destroy RPCs for Multiplay guests and linking
    // player GameObjects to player entities based on Multiplay connectionID
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateAfter(typeof(MultiplayInitSystem))]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class MultiplayPlayerSystem : SystemBase
    {
        private EntityQuery playerQuery;
        protected override void OnCreate()
        {
            playerQuery= new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<Authoring.Player>()
                .WithAll<NewPlayer>()
                .Build(this);
            RequireForUpdate<PlayerSpawner>();
        }
        protected override void OnUpdate()
        {
            Multiplay multiplay = MultiplaySingleton.Instance;
            if (multiplay.IsUnityNull())
                return;
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            NativeArray<Authoring.Player> playerData = playerQuery.ToComponentDataArray<Authoring.Player>(Allocator.Temp);
            NativeArray<Entity> newPlayerEntities = playerQuery.ToEntityArray(Allocator.Temp);
            var playerSpawner = SystemAPI.GetSingleton<PlayerSpawner>();
            foreach (var (connID, playerObj) in multiplay.connectionPlayerObjects)
            {
                var playerController = playerObj.GetComponent<MultiplayPlayerController>();
                
                // Check if a newly spawned player is a response to this playerObject's request
                if (playerController.playerEntityRequestSent)
                {
                    for (int i = 0; i < newPlayerEntities.Length; i++)
                    {
                        var player = playerData[i];
                        // todo: surely there is a better way of linking player entity and object...
                        if (player.Username == playerController.username)
                        {
                            
                            var playerEntity = newPlayerEntities[i];
                            playerController.playerEntityRequestSent = false;
                            playerController.playerEntityExists = true;
                            Debug.Log($"Linking player entity {playerEntity } to {connID}");
                            playerController.playerEntity = playerEntity;

                            // Store connectionID in components as a blob reference.
                            var builder = new BlobBuilder(Allocator.Temp);
                            ref BlobString blobString = ref builder.ConstructRoot<BlobString>();
                            builder.AllocateString(ref blobString, connID);
                            // Copy new player component
                            commandBuffer.SetComponent(playerEntity, new Authoring.Player
                            {
                                PlayerConfig = player.PlayerConfig,
                                Velocity = player.Velocity,
                                OnGround = player.OnGround,
                                JumpStart = player.JumpStart,
                                Username = player.Username,
                                multiplayConnectionID = builder.CreateBlobAssetReference<BlobString>(Allocator.Persistent)
                            });
                            builder.Dispose();
                            // Create a new block outline entity. Used by the HighlightSelectedBlockSystem on clients
                            commandBuffer.Instantiate(playerSpawner.BlockOutline);
                            commandBuffer.SetComponentEnabled<NewPlayer>(playerEntity, false);
                            // Color the player red since it is locally controlled
                            commandBuffer.SetComponent(playerEntity,
                                new URPMaterialPropertyBaseColor() { Value = new float4(1, 0, 0, 1) });
                        }
                    }
                }

                // Send any necessary player spawn requests
                if (playerController.inputStart && 
                    !playerController.playerEntityExists &&
                    !playerController.playerEntityRequestSent)
                {
                    // Create a spawn player rpc
                    foreach (var (id, entity) in SystemAPI.Query<RefRO<NetworkId>>().WithEntityAccess()
                                 .WithAll<NetworkStreamInGame>())
                    {
                        var req = commandBuffer.CreateEntity();
                        SpawnPlayerRequest spawnPlayerRequest = new SpawnPlayerRequest
                            { Username = playerController.username };
                        commandBuffer.AddComponent(req, spawnPlayerRequest);
                        Debug.Log($"Sending spawn player RPC for user {playerController.username}");
                        commandBuffer.AddComponent(req, new SendRpcCommandRequest { TargetConnection = entity });
                        playerController.playerEntityRequestSent = true;
                    }
                }
            }
            
            // Handle disconnected Multiplay connections
            foreach (var connectionId in multiplay.disconnectedIds)
            {
                var playerController = multiplay.connectionPlayerObjects[connectionId]
                    .GetComponent<MultiplayPlayerController>();

                Debug.Log($"Creating DestroyPlayer RPC for entity {playerController.playerEntity} on {connectionId}");
                foreach (var (_, entity) in SystemAPI.Query<RefRO<NetworkId>>().WithAll<NetworkStreamInGame>().WithEntityAccess())
                {
                    var req = commandBuffer.CreateEntity();
                    DestroyPlayerRequest destroyPlayerRequest = new DestroyPlayerRequest
                        { Player = playerController.playerEntity };
                    commandBuffer.AddComponent(req, destroyPlayerRequest);
                    commandBuffer.AddComponent(req, new SendRpcCommandRequest { TargetConnection = entity });
                }
                multiplay.DestroyMultiplayConnection(connectionId);
            }

            multiplay.disconnectedIds.Clear();

            commandBuffer.Playback(EntityManager);

        }
    }
}
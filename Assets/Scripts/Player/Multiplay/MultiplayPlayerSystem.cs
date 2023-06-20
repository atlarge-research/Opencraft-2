using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.RenderStreaming;
using Unity.VisualScripting;
using UnityEngine;

// ECS System wrapper around Multiplay class that handles render streaming connections
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateAfter(typeof(MultiplayInitSystem))]
public partial class MultiplayPlayerSystem: SystemBase
{
    protected override void OnUpdate()
    {
        // Begin multiplay hosts in OnUpdate to ensure the GameObjects it references are properly initialized
        Multiplay multiplay = MultiplaySingleton.Instance;
        if (multiplay.IsUnityNull())
            return;
        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        
        // Send any necessary player spawn requests
        foreach(var (connID,playerObj) in multiplay.connectionPlayerObjects)
        {
            var playerController = playerObj.GetComponent<MultiplayPlayerController>();
            if (playerController.inputStart && !playerController.playerEntityExists && !playerController.playerEntityRequestSent)
            {
                // There is only one NetworkID component on clients.
                foreach (var (id, entity) in SystemAPI.Query<RefRO<NetworkId>>().WithEntityAccess().WithAll<NetworkStreamInGame>())
                {
                    var req = commandBuffer.CreateEntity();
                    SpawnPlayerRequest spawnPlayerRequest= new SpawnPlayerRequest { Username = playerController.Username };
                    commandBuffer.AddComponent(req, spawnPlayerRequest);
                    Debug.Log($"Sending spawn player RPC for user {playerController.Username}");
                    commandBuffer.AddComponent(req, new SendRpcCommandRequest { TargetConnection = entity });
                    playerController.playerEntityRequestSent = true;
                }
            }
        }
        
        // Handle any spawned new players
        foreach (var (player, entity) in SystemAPI.Query<RefRW<Player>>().WithEntityAccess().WithAll<NewPlayer>())
        {
            // Compare any new player's username to player object usernames
            // todo: surely there is a better way of linking player entity and object...
            foreach (var (connID, playerObj) in multiplay.connectionPlayerObjects)
            {
                var playerController = playerObj.GetComponent<MultiplayPlayerController>();
                if (playerController.playerEntityRequestSent)
                {
                    if (playerController.Username == player.ValueRO.Username)
                    {
                        playerController.playerEntityRequestSent = false;
                        playerController.playerEntityExists = true;
                        Debug.Log($"Linking player entity {entity} to {connID}");
                        playerController.playerEntity = entity;
                        
                        // Store connectionID in components as a blob reference.
                        var builder = new BlobBuilder(Allocator.Temp);
                        ref BlobString blobString = ref builder.ConstructRoot<BlobString>();
                        builder.AllocateString(ref blobString, connID);
                        player.ValueRW.multiplayConnectionID = builder.CreateBlobAssetReference<BlobString>(Allocator.Persistent);
                        builder.Dispose();
                        commandBuffer.SetComponentEnabled<NewPlayer>(entity, false);
                    }
                }
            }
        }
        
        // Handle disconnected Multiplay connections
        foreach (var connectionId in multiplay.disconnectedIds)
        {
            var playerController = multiplay.connectionPlayerObjects[connectionId].GetComponent<MultiplayPlayerController>();
            
            Debug.Log($"Creating DestroyPlayer RPC for entity {playerController.playerEntity} on {connectionId}");
            foreach (var (id, entity) in SystemAPI.Query<RefRO<NetworkId>>().WithEntityAccess().WithAll<NetworkStreamInGame>())
            {
                var req = commandBuffer.CreateEntity();
                DestroyPlayerRequest destroyPlayerRequest= new DestroyPlayerRequest { Player = playerController.playerEntity};
                commandBuffer.AddComponent(req, destroyPlayerRequest);
                commandBuffer.AddComponent(req, new SendRpcCommandRequest { TargetConnection = entity });
            }
            // The connection ID is now invalid, dispose it.
            var p = EntityManager.GetComponentData<Player>(playerController.playerEntity);
            p.multiplayConnectionID.Dispose();
            multiplay.DestroyMultiplayConnection(connectionId);
        }
        multiplay.disconnectedIds.Clear();
        
        commandBuffer.Playback(EntityManager);

    }
}
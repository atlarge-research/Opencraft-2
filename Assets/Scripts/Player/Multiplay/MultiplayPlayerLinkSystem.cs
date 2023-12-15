using Opencraft.Player.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Rendering;
using Unity.VisualScripting;
using UnityEngine;

/*
 * Links player entities and objects 
 */
namespace Opencraft.Player.Multiplay
{
    // Run on Multiplay hosts, handles sending player spawn/destroy RPCs for Multiplay guests and linking
    // player GameObjects to player entities based on Multiplay connectionID
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateAfter(typeof(PolkaDOTS.Multiplay.MultiplayInitSystem))]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class MultiplayPlayerLinkSystem : SystemBase
    {
        private EntityQuery playerQuery;
        protected override void OnCreate()
        {
            playerQuery= new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<PolkaDOTS.Player>()
                .WithAll<PolkaDOTS.NewPlayer>()
                .WithAll<GhostOwnerIsLocal>()
                .Build(this);
            if (!World.Unmanaged.IsSimulatedClient())
            {
                RequireForUpdate<PlayerSpawner>();
            }
        }
        protected override void OnUpdate()
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            
            // Simulated players do not need to deal with objects
            if (World.Unmanaged.IsSimulatedClient())
            {
                // Create a spawn player rpc
                foreach (var (id, netEntity) in SystemAPI.Query<RefRO<NetworkId>>().WithEntityAccess()
                             .WithAll<NetworkStreamInGame>())
                {
                    var req = commandBuffer.CreateEntity();
                    FixedString32Bytes name = new FixedString32Bytes(World.Unmanaged.Name);
                    var spawnPlayerRequest = new SpawnPlayerRequest
                        { Username =  name };
                    commandBuffer.AddComponent(req, spawnPlayerRequest);
                    Debug.Log($"Sending spawn player RPC for user { name }");
                    commandBuffer.AddComponent(req, new SendRpcCommandRequest { TargetConnection = netEntity });
                    Enabled = false;
                }
                
                commandBuffer.Playback(EntityManager);
              
                return;
            }
            
            PolkaDOTS.Multiplay.Multiplay multiplay = PolkaDOTS.Multiplay.MultiplaySingleton.Instance;
            if (multiplay.IsUnityNull())
                return;
            
            
            
            var playerSpawner = SystemAPI.GetSingleton<PlayerSpawner>();
            
            foreach (var (connID, playerObj) in multiplay.connectionPlayerObjects)
            {
                var playerController = playerObj.GetComponent<MultiplayPlayerController>();
                
                // Check if a player has a spawned player with the same name, link to it if it exists
                if (playerController.playerEntityRequestSent && !playerController.playerEntityExists)
                {
                    if(linkPlayerIfExists(ref playerController, ref commandBuffer, in playerSpawner, in connID)){
                        playerController.playerEntityRequestSent = false;
                        playerController.playerEntityExists = true;
                    }
                }

                // Send any necessary player spawn requests
                if (!playerController.playerEntityExists &&
                    !playerController.playerEntityRequestSent)
                {
                    // If this player controller is a guest player, use their username instead of the local one
                    if (connID != "LOCALPLAYER")
                    {
                        playerController.username = connID;
                    }
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

                if (playerController.playerEntityExists)
                {
                    commandBuffer.SetComponentEnabled<GhostOwnerIsLocal>(playerController.playerEntity, false);
                    /*Debug.Log($"Creating DestroyPlayer RPC for entity {playerController.playerEntity} on {connectionId}");
                    foreach (var (_, entity) in SystemAPI.Query<RefRO<NetworkId>>().WithAll<NetworkStreamInGame>().WithEntityAccess())
                    {
                        var req = commandBuffer.CreateEntity();
                        DestroyPlayerRequest destroyPlayerRequest = new DestroyPlayerRequest
                            { Player = playerController.playerEntity };
                        commandBuffer.AddComponent(req, destroyPlayerRequest);
                        commandBuffer.AddComponent(req, new SendRpcCommandRequest { TargetConnection = entity });
                    }*/
                }
                multiplay.DestroyMultiplayConnection(connectionId);
            }

            multiplay.disconnectedIds.Clear();

            commandBuffer.Playback(EntityManager);

        }

        bool linkPlayerIfExists(ref MultiplayPlayerController playerController, ref EntityCommandBuffer commandBuffer, in PlayerSpawner playerSpawner, in string connID)
        {
            NativeArray<PolkaDOTS.Player> playerData = playerQuery.ToComponentDataArray<PolkaDOTS.Player>(Allocator.Temp);
            NativeArray<Entity> playerEntities = playerQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < playerEntities.Length; i++)
            {
                var player = playerData[i];
                var playerEntity = playerEntities[i];
                // todo: surely there is a better way of linking player entity and object...
                if (player.Username == playerController.username)
                {
                    Debug.Log($"Linking player entity {playerEntity} to {playerController.username}@{connID}");
                    playerController.playerEntity = playerEntity;

                    // Store connectionID in components as a blob reference.
                    var builder = new BlobBuilder(Allocator.Temp);
                    ref BlobString blobString = ref builder.ConstructRoot<BlobString>();
                    builder.AllocateString(ref blobString, connID);
                    // Copy new player component
                    commandBuffer.SetComponent(playerEntity, new PolkaDOTS.Player
                    {
                        JumpVelocity = player.JumpVelocity,
                        Username = player.Username,
                        multiplayConnectionID = builder.CreateBlobAssetReference<BlobString>(Allocator.Persistent)
                    });
                    builder.Dispose();
                    // Create a new block outline entity. Used by the HighlightSelectedBlockSystem on clients
                    commandBuffer.Instantiate(playerSpawner.BlockOutline);
                    commandBuffer.SetComponentEnabled<PolkaDOTS.NewPlayer>(playerEntity, false);
                    
                    if (playerController.username != "LOCALPLAYER")
                        commandBuffer.AddComponent<PolkaDOTS.GuestPlayer>(playerEntity);
                    
                    // Color the player red since it is locally controlled
                    commandBuffer.SetComponent(playerEntity,
                        new URPMaterialPropertyBaseColor() { Value = new float4(1, 0, 0, 1) });
                    return true;
                }
            }

            return false;
        }
        
    }
    
    
    /*// Stub version of the link system run on thin clients
    [WorldSystemFilter(WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class SimulatedClientPlayerLinkSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<NetworkStreamInGame>();
            RequireForUpdate<PlayerSpawner>(); // Don't start until scene has been loaded
        }
        protected override void OnUpdate()
        {

            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            
            // Create a spawn player rpc
            foreach (var (id, netEntity) in SystemAPI.Query<RefRO<NetworkId>>().WithEntityAccess()
                         .WithAll<NetworkStreamInGame>())
            {
                var req = commandBuffer.CreateEntity();
                FixedString32Bytes name = new FixedString32Bytes(World.Unmanaged.Name);
                var spawnPlayerRequest = new SpawnPlayerRequest
                    { Username =  name };
                commandBuffer.AddComponent(req, spawnPlayerRequest);
                Debug.Log($"Sending spawn player RPC for user { name }");
                commandBuffer.AddComponent(req, new SendRpcCommandRequest { TargetConnection = netEntity });
            }
        

            commandBuffer.Playback(EntityManager);
            Enabled = false;

        }
        
    }*/
}
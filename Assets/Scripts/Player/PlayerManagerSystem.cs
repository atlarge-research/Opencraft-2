using Opencraft.Player.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;


namespace Opencraft.Player
{

    // When server receives spawner player request, check if its valid, spawn player, then delete request
    // If a player is marked for destruction, destroy it!
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateAfter(typeof(PolkaDOTS.Networking.StartGameStreamServerSystem))]
    public partial class  PlayerManagerSystem : SystemBase
    {
        private BeginSimulationEntityCommandBufferSystem m_CommandBufferSystem;
        private EntityQuery playerQuery;
        protected override void OnCreate()
        {
            RequireForUpdate<PlayerSpawner>();
            // Only update if there are requests to handle
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ReceiveRpcCommandRequest>()
                .WithAny<SpawnPlayerRequest,DestroyPlayerRequest >();
            RequireForUpdate(GetEntityQuery(builder));
            
            playerQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PolkaDOTS.Player>()
                .WithAll<LocalTransform>()
                .WithAll<PlayerInput>()
                .Build(this);
            m_CommandBufferSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
        }

        [BurstCompile]
        protected override void OnUpdate()
        {
            var prefab = SystemAPI.GetSingleton<PlayerSpawner>().Player;
            EntityManager.GetName(prefab, out var prefabName);

            var commandBuffer = m_CommandBufferSystem.CreateCommandBuffer();
            ComponentLookup<NetworkId> networkIdFromEntity = GetComponentLookup<NetworkId>(true);
            
            // Existing player data
            NativeArray<PolkaDOTS.Player> playerData = playerQuery.ToComponentDataArray<PolkaDOTS.Player>(Allocator.Temp);
            //NativeArray<LocalTransform> playerLocs = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            //NativeArray<PlayerInput> playerInputs = playerQuery.ToComponentDataArray<PlayerInput>(Allocator.Temp);
            NativeArray<Entity> playerEntities = playerQuery.ToEntityArray(Allocator.Temp);
            
            Entities.WithName("HandleSpawnPlayerRPCs").ForEach((Entity entity,
                in ReceiveRpcCommandRequest requestSource, in SpawnPlayerRequest reqSpawn) =>
            {
                NetworkId networkId = networkIdFromEntity[requestSource.SourceConnection];

                bool found = false;
                //LocalTransform playerLoc = LocalTransform.Identity;
                //PlayerInput playerInput = default;
                
                // Check if player for this username already exists
                for (int i = 0; i < playerData.Length; i++)
                {
                    PolkaDOTS.Player player = playerData[i];
                    if (player.Username == reqSpawn.Username)
                    {
                        var playerEntity = playerEntities[i];
                        //playerLoc  = playerLocs[i];
                        //playerInput = playerInputs[i];
                        //commandBuffer.DestroyEntity(playerEntity); // Destroy the previous player entity
                        
                        Debug.Log(
                            $"Linking user '{reqSpawn.Username}@conn{networkId.Value}' to existing player!");
                        commandBuffer.SetComponent(playerEntity, new GhostOwner { NetworkId = networkId.Value });
                        //commandBuffer.SetComponent(playerEntity, player);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    Debug.Log(
                        $"'Spawning a Ghost '{prefabName}' for user '{reqSpawn.Username}' on connection '{networkId.Value}'!");
                    commandBuffer.AddComponent<ConnectionState>(entity);
                    var player = commandBuffer.Instantiate(prefab);
                    commandBuffer.SetComponent(player, new GhostOwner { NetworkId = networkId.Value });
                    commandBuffer.SetComponent(player, new PolkaDOTS.Player{ Username = reqSpawn.Username });
                    // Move spawned player object to set position
                    commandBuffer.SetComponent(player,
                        LocalTransform.FromPosition(new float3(0.5f, 30f , 0.5f)));
                    
                }


                commandBuffer.DestroyEntity(entity);
            }).Run(); // On main thread, unlikely enough players will connect on same frame to require parallel execution
            
            // DestroyPlayerRPC only used during Multiplay, otherwise a client DC will automatically destroy the associated
            // players through Netcode for Entities
            Entities.WithName("HandleDestroyPlayerRPCs").ForEach((Entity entity,
                in ReceiveRpcCommandRequest _, in DestroyPlayerRequest reqDestroy) =>
            {
                Debug.Log($"Destroying player entity '{reqDestroy.Player}'!");
                commandBuffer.DestroyEntity(reqDestroy.Player);
                commandBuffer.DestroyEntity(entity);
            }).Run();

            m_CommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
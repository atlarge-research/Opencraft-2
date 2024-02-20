using Opencraft.Player.Authoring;
using Opencraft.Terrain.Authoring;
using PolkaDOTS;
using Unity.Entities;
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
        private WorldParameters _worldParameters;
        protected override void OnCreate()
        {
            RequireForUpdate<PlayerSpawner>();
            RequireForUpdate<TerrainSpawner>();
            //RequireForUpdate<WorldParameters>();
            RequireForUpdate<PlayerSpawn>();
            // Only update if there are requests to handle
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ReceiveRpcCommandRequest>()
                .WithAny<SpawnPlayerRequest,DestroyPlayerRequest >();
            RequireForUpdate(GetEntityQuery(builder));
            
            playerQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PlayerComponent>()
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
            
            var playerSpawn = SystemAPI.GetSingleton<PlayerSpawn>();

            var commandBuffer = m_CommandBufferSystem.CreateCommandBuffer();
            ComponentLookup<NetworkId> networkIdFromEntity = GetComponentLookup<NetworkId>(true);
            
            // Existing player data
            NativeArray<PlayerComponent> playerData = playerQuery.ToComponentDataArray<PlayerComponent>(Allocator.Temp);
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
                    PlayerComponent playerComponent = playerData[i];
                    if (playerComponent.Username == reqSpawn.Username)
                    {
                        var playerEntity = playerEntities[i];
                        //playerLoc  = playerLocs[i];
                        //playerInput = playerInputs[i];
                        //commandBuffer.DestroyEntity(playerEntity); // Destroy the previous player entity
                        
                        Debug.Log(
                            $"Linking user '{reqSpawn.Username}@conn{networkId.Value}' to existing player!");
                        commandBuffer.SetComponent(playerEntity, new GhostOwner { NetworkId = networkId.Value });
                        commandBuffer.SetComponentEnabled<PlayerInGame>(playerEntity, true);
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
                    commandBuffer.SetComponent(player, new PlayerComponent{ Username = reqSpawn.Username });
                    commandBuffer.SetComponentEnabled<PlayerInGame>(player, true);
                    // Move spawned player object to set position
                    
                    commandBuffer.SetComponent(player,
                        LocalTransform.FromPosition(playerSpawn.location));
                    
                }


                commandBuffer.DestroyEntity(entity);
            }).Run(); // On main thread, unlikely enough players will connect on same frame to require parallel execution
            
            // DestroyPlayerRPC only used during Multiplay
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
using Opencraft.Player.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;


namespace Opencraft.Player
{

    // When server receives spawner player request, check if its valid, spawn player, then delete request
    // If a player is marked for destruction, destroy it!
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class  PlayerManagerSystem : SystemBase
    {
        private BeginSimulationEntityCommandBufferSystem m_CommandBufferSystem;
        
        protected override void OnCreate()
        {
            RequireForUpdate<PlayerSpawner>();
            // Only update if there are requests to handle
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ReceiveRpcCommandRequest>()
                .WithAny<SpawnPlayerRequest,DestroyPlayerRequest >();
            RequireForUpdate(GetEntityQuery(builder));
            m_CommandBufferSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
        }

        [BurstCompile]
        protected override void OnUpdate()
        {
            var prefab = SystemAPI.GetSingleton<PlayerSpawner>().Player;
            EntityManager.GetName(prefab, out var prefabName);

            var commandBuffer = m_CommandBufferSystem.CreateCommandBuffer();
            ComponentLookup<NetworkId> networkIdFromEntity = GetComponentLookup<NetworkId>(true);

            Entities.WithName("HandleSpawnPlayerRPCs").ForEach((Entity entity,
                in ReceiveRpcCommandRequest requestSource, in SpawnPlayerRequest reqSpawn) =>
            {
                NetworkId networkId = networkIdFromEntity[requestSource.SourceConnection];
                UnityEngine.Debug.Log(
                    $"'Spawning a Ghost '{prefabName}' for user '{reqSpawn.Username}' on connection '{networkId.Value}'!");
                commandBuffer.AddComponent<ConnectionState>(entity);
                var player = commandBuffer.Instantiate(prefab);
                commandBuffer.SetComponent(player, new GhostOwner { NetworkId = networkId.Value });
                commandBuffer.SetComponent(player, new Authoring.Player { Username = reqSpawn.Username });

                // Add the player to the linked entity group so it is destroyed automatically on disconnect
                commandBuffer.AppendToBuffer(requestSource.SourceConnection, new LinkedEntityGroup { Value = player });

                // Give each NetworkId their own spawn pos:
                {
                    var isEven = (networkId.Value & 1) == 0;
                    const float halfCharacterWidthPlusHalfPadding = .55f;
                    const float spawnStaggeredOffset = 0.25f;
                    var staggeredXPos =
                        networkId.Value * math.@select(halfCharacterWidthPlusHalfPadding,
                            -halfCharacterWidthPlusHalfPadding, isEven) +
                        math.@select(-spawnStaggeredOffset, spawnStaggeredOffset, isEven);
                    var preventZFighting = 2.5f + -0.01f * networkId.Value;

                    commandBuffer.SetComponent(player,
                        LocalTransform.FromPosition(new float3(staggeredXPos, preventZFighting, -1)));
                }
                commandBuffer.DestroyEntity(entity);
            }).Run(); // On main thread, unlikely enough players will connect on same frame to require parallel execution
            
            // DestroyPlayerRPC only used during Multiplay, otherwise a client DC will automatically destroy the associated
            // players through Netcode for Entities
            Entities.WithName("HandleDestroyPlayerRPCs").ForEach((Entity entity,
                in ReceiveRpcCommandRequest _, in DestroyPlayerRequest reqDestroy) =>
            {
                UnityEngine.Debug.Log($"Destroying player entity '{reqDestroy.Player}'!");
                commandBuffer.DestroyEntity(reqDestroy.Player);
                commandBuffer.DestroyEntity(entity);
            }).Run();

            m_CommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
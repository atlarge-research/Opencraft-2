using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Logging;
using Unity.Networking.Transport;
using UnityEngine.UIElements;

public struct SpawnPlayerRequest : IRpcCommand
{
    public int Username;
}

public struct DestroyPlayerRequest : IRpcCommand
{
    public Entity Player;
}

// When server receives spawner player request, check if its valid, spawn player, then delete request
// If a player is marked for destruction, destroy it!
[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct PlayerSpawnerSystem : ISystem
{
    private ComponentLookup<NetworkId> networkIdFromEntity;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerSpawner>();
        networkIdFromEntity = state.GetComponentLookup<NetworkId>(true);
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var prefab = SystemAPI.GetSingleton<PlayerSpawner>().Player;
        state.EntityManager.GetName(prefab, out var prefabName);
        var worldName = state.WorldUnmanaged.Name;

        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        networkIdFromEntity.Update(ref state);

        foreach (var (reqSrc,reqSpawn, reqEntity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<SpawnPlayerRequest>>()
                     .WithEntityAccess())
        {
            NetworkId networkId = networkIdFromEntity[reqSrc.ValueRO.SourceConnection];

            UnityEngine.Debug.Log($"'{worldName}' spawning a Ghost '{prefabName}' for user '{reqSpawn.ValueRO.Username}' on connection '{networkId.Value}'!");
            
            var player = commandBuffer.Instantiate(prefab);
            commandBuffer.SetComponent(player, new GhostOwner { NetworkId = networkId.Value});
            commandBuffer.SetComponent(player, new Player { Username = reqSpawn.ValueRO.Username});

            // Add the player to the linked entity group so it is destroyed automatically on disconnect
            commandBuffer.AppendToBuffer(reqSrc.ValueRO.SourceConnection, new LinkedEntityGroup{Value = player});

            // Give each NetworkId their own spawn pos:
            {
                var isEven = (networkId.Value & 1) == 0;
                const float halfCharacterWidthPlusHalfPadding = .55f;
                const float spawnStaggeredOffset = 0.25f;
                var staggeredXPos = networkId.Value * math.@select(halfCharacterWidthPlusHalfPadding, -halfCharacterWidthPlusHalfPadding, isEven) + math.@select(-spawnStaggeredOffset, spawnStaggeredOffset, isEven);
                var preventZFighting = 2.5f + -0.01f * networkId.Value;

                commandBuffer.SetComponent(player, LocalTransform.FromPosition(new float3(staggeredXPos, preventZFighting, 0)));
            }
            commandBuffer.DestroyEntity(reqEntity);
        }
        
        // Handle MultiPlay player destruction on server to propagate it to all other clients.
        foreach (var (reqSrc,reqDestroy, reqEntity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<DestroyPlayerRequest>>()
                     .WithEntityAccess())
        {

            UnityEngine.Debug.Log($"'{worldName}' destroying player entity '{reqDestroy.ValueRO.Player}''!");
            commandBuffer.DestroyEntity(reqDestroy.ValueRO.Player);
            commandBuffer.DestroyEntity(reqEntity);
        }
        commandBuffer.Playback(state.EntityManager);

    }
}
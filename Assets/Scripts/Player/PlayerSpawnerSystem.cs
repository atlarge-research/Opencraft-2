using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Logging;
using Unity.Networking.Transport;

public struct SpawnPlayerRequest : IRpcCommand
{
}

public struct PlayerSpawned : IComponentData
{
}

// When server receives spawner player request, check if its valid, spawn player, then delete request
[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct PlayerSpawnerSystem : ISystem
{
    private ComponentLookup<NetworkId> networkIdFromEntity;
    private ComponentLookup<PlayerSpawned> playerSpawnedFromEntity;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerSpawner>();
        var builder = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<SpawnPlayerRequest>()
            .WithAll<ReceiveRpcCommandRequest>();
        state.RequireForUpdate(state.GetEntityQuery(builder)); // Only run when there are RPCs to be processed
        networkIdFromEntity = state.GetComponentLookup<NetworkId>(true);
        playerSpawnedFromEntity = state.GetComponentLookup<PlayerSpawned>(true);
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var prefab = SystemAPI.GetSingleton<PlayerSpawner>().Player;
        state.EntityManager.GetName(prefab, out var prefabName);
        var worldName = state.WorldUnmanaged.Name;

        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        networkIdFromEntity.Update(ref state);
        playerSpawnedFromEntity.Update(ref state);
        
        // Fixed size at 1024 could overflow if enough players send a request on the same tick
        var connectionsSeen = CollectionHelper.CreateNativeArray<Entity>(1024, Allocator.Temp);
        int index = 0;

        foreach (var (reqSrc, reqEntity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
                     .WithAll<SpawnPlayerRequest>().WithEntityAccess())
        {
            index++;
            if (!checkIfValid(reqSrc, connectionsSeen))
            {
                // This connection already has a player
                commandBuffer.DestroyEntity(reqEntity);
                continue;
            }

            connectionsSeen[index] = reqSrc.ValueRO.SourceConnection;
            var networkId = networkIdFromEntity[reqSrc.ValueRO.SourceConnection];
            
            commandBuffer.AddComponent<PlayerSpawned>(reqSrc.ValueRO.SourceConnection);

            UnityEngine.Debug.Log($"'{worldName}' spawning a Ghost '{prefabName}' for connection '{networkId.Value}'!");
            
            var player = commandBuffer.Instantiate(prefab);
            commandBuffer.SetComponent(player, new GhostOwner { NetworkId = networkId.Value});

            // Add the player to the linked entity group so it is destroyed automatically on disconnect
            commandBuffer.AppendToBuffer(reqSrc.ValueRO.SourceConnection, new LinkedEntityGroup{Value = player});

            // Give each NetworkId their own spawn pos:
            {
                var isEven = (networkId.Value & 1) == 0;
                const float halfCharacterWidthPlusHalfPadding = .55f;
                const float spawnStaggeredOffset = 0.25f;
                var staggeredXPos = networkId.Value * math.@select(halfCharacterWidthPlusHalfPadding, -halfCharacterWidthPlusHalfPadding, isEven) + math.@select(-spawnStaggeredOffset, spawnStaggeredOffset, isEven);
                var preventZFighting = 2 + -0.01f * networkId.Value;

                commandBuffer.SetComponent(player, LocalTransform.FromPosition(new float3(staggeredXPos, preventZFighting, 0)));

            }
            commandBuffer.DestroyEntity(reqEntity);
        }
        commandBuffer.Playback(state.EntityManager);

    }

    private bool checkIfValid(RefRO<ReceiveRpcCommandRequest> reqSrc, NativeArray<Entity> connectionsSeen)
    {
        bool flag = true;
        foreach (var conn in connectionsSeen )
        {
            if (conn.Equals(reqSrc.ValueRO.SourceConnection))
            {
                // This connection has already had a request processed
                flag = false;
                break;
            }
        }
        
        if (playerSpawnedFromEntity.TryGetComponent(reqSrc.ValueRO.SourceConnection, out PlayerSpawned playerSpawned))
        {
            // This connection already has a player
            flag = false;
        }
        return flag;
    }
}
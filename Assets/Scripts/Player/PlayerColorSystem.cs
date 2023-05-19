using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Rendering;



[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[BurstCompile]
public partial struct PlayerColorSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Player>();
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var commandBuffer = new EntityCommandBuffer(state.WorldUpdateAllocator);
        foreach (var ( player, entity) in SystemAPI.Query<RefRO<Player>>().WithAll<GhostOwnerIsLocal>().WithEntityAccess())
        {
            commandBuffer.SetComponent(entity, new URPMaterialPropertyBaseColor() { Value = new float4(1, 0, 0, 1) });
        }
        commandBuffer.Playback(state.EntityManager);
    }
}
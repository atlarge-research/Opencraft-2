using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public struct Player : IComponentData
{
    public Entity PlayerConfig;

    [GhostField(Quantization = 1000)]
    public float3 Velocity;
    [GhostField]
    public byte OnGround;
    [GhostField]
    public NetworkTick JumpStart;
}

[GhostComponent(PrefabType=GhostPrefabType.AllPredicted, OwnerSendType = SendToOwnerType.SendToNonOwner)]
public struct PlayerInput : IInputComponentData
{
    [GhostField] public float2 Movement;
    [GhostField] public InputEvent Jump;
    [GhostField] public InputEvent PrimaryAction;
    [GhostField] public InputEvent SecondaryAction;
    [GhostField] public float Pitch;
    [GhostField] public float Yaw;
}

// Group player related component accessors for ease of use
public readonly partial struct PlayerAspect : IAspect
{
    public readonly Entity Self;
    public readonly RefRW<LocalTransform> Transform;

    readonly RefRO<AutoCommandTarget> m_AutoCommandTarget;
    readonly RefRW<Player> m_Character;
    readonly RefRW<PhysicsVelocity> m_Velocity;
    readonly RefRO<PlayerInput> m_Input;
    readonly RefRO<GhostOwner> m_Owner;

    public AutoCommandTarget AutoCommandTarget => m_AutoCommandTarget.ValueRO;
    public PlayerInput Input => m_Input.ValueRO;
    public int OwnerNetworkId => m_Owner.ValueRO.NetworkId;
    public ref Player Player => ref m_Character.ValueRW;
    public ref PhysicsVelocity Velocity => ref m_Velocity.ValueRW;
}

[DisallowMultipleComponent]
public class PlayerAuthoring : MonoBehaviour
{
    public PlayerConfigAuthoring playerConfig;
    class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new PlayerInput());
            AddComponent(entity, new Player
            {
                PlayerConfig = GetEntity(authoring.playerConfig.gameObject, TransformUsageFlags.Dynamic)
            });
        }
    }
}
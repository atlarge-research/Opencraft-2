using PolkaDOTS;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Opencraft.Player.Authoring
{
    
    public struct PlayerContainingArea : IComponentData
    {
        // Link to containing area
        public Entity Area;
        // Where that area is
        public int3 AreaLocation;
    }
    
    // Component marking this entity as having a specific block selected.
    // Neighbor block refers to the block neighbor of the selected block closest to this entity
    public struct SelectedBlock : IComponentData
    {
        public Entity terrainArea;
        public int3 blockLoc;
        public Entity neighborTerrainArea;
        public int3 neighborBlockLoc;
    }

    
    // All of a player's input for a frame, uses special component type IInputComponentData
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted, OwnerSendType = SendToOwnerType.SendToNonOwner)]
    public struct PlayerInput : IInputComponentData
    {
        [GhostField]public float2 Movement;
        [GhostField]public InputEvent Jump;
        [GhostField]public InputEvent PrimaryAction;
        [GhostField]public InputEvent SecondaryAction;
        [GhostField]public float Pitch;
        [GhostField]public float Yaw;
    }

    // Group player related component accessors for ease of use
    public readonly partial struct PlayerAspect : IAspect
    {
        public readonly Entity Self; // Special self reference, only allowed on Aspects
        public readonly RefRW<LocalTransform> Transform;

        readonly RefRO<AutoCommandTarget> m_AutoCommandTarget;
        readonly RefRW<PolkaDOTS.Player> m_Character;
        //readonly RefRW<PhysicsVelocity> m_Velocity;
        readonly RefRO<PlayerInput> m_Input;
        readonly RefRO<GhostOwner> m_Owner;
        readonly RefRW<SelectedBlock> m_SelectedBlock;
        readonly RefRW<PlayerContainingArea> m_Area;

        public AutoCommandTarget AutoCommandTarget => m_AutoCommandTarget.ValueRO;
        public PlayerInput Input => m_Input.ValueRO;
        public int OwnerNetworkId => m_Owner.ValueRO.NetworkId;
        public ref PolkaDOTS.Player Player => ref m_Character.ValueRW;

        public ref PlayerContainingArea ContainingArea => ref m_Area.ValueRW;
        
        public ref SelectedBlock SelectedBlock => ref m_SelectedBlock.ValueRW;
    }

    [DisallowMultipleComponent]
    public class PlayerAuthoring : MonoBehaviour
    {
        //public PlayerConfigAuthoring playerConfig;
        class Baker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PlayerInput());
                AddComponent(entity, new PolkaDOTS.Player());
                AddComponent(entity, new PolkaDOTS.NewPlayer());
                AddComponent(entity, new PlayerContainingArea());
                AddComponent(entity, new SelectedBlock());
                AddComponent(entity, new PlayerInGame());
            }
        }
    }
}
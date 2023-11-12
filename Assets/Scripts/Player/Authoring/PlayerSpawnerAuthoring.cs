using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.VisualScripting;
using UnityEngine;

namespace Opencraft.Player.Authoring
{
    public struct PlayerSpawner : IComponentData, ISingleton
    {
        public Entity Player;
        public Entity BlockOutline;
    }
    
    public struct SpawnPlayerRequest : IRpcCommand
    {
        public FixedString32Bytes Username;
    }

    public struct DestroyPlayerRequest : IRpcCommand
    {
        public Entity Player;
    }
    
    // Create a player spawner singleton component
    [DisallowMultipleComponent]
    public class PlayerSpawnerAuthoring : MonoBehaviour
    {
        public GameObject Player;
        public GameObject BlockOutline;


        class Baker : Baker<PlayerSpawnerAuthoring>
        {
            public override void Bake(PlayerSpawnerAuthoring authoring)
            {
                PlayerSpawner component = default(PlayerSpawner);
                component.Player = GetEntity(authoring.Player, TransformUsageFlags.Dynamic);
                component.BlockOutline = GetEntity(authoring.BlockOutline, TransformUsageFlags.Dynamic);

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, component);
            }
        }
    }
}
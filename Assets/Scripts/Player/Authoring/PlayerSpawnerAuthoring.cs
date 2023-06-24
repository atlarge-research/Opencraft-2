using Unity.Entities;
using Unity.NetCode;
using Unity.VisualScripting;
using UnityEngine;

namespace Opencraft.Player.Authoring
{
    public struct PlayerSpawner : IComponentData, ISingleton
    {
        public Entity Player;
    }
    
    public struct SpawnPlayerRequest : IRpcCommand
    {
        public int Username;
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


        class Baker : Baker<PlayerSpawnerAuthoring>
        {
            public override void Bake(PlayerSpawnerAuthoring authoring)
            {
                PlayerSpawner component = default(PlayerSpawner);
                component.Player = GetEntity(authoring.Player, TransformUsageFlags.Dynamic);

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, component);
            }
        }
    }
}
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public struct PlayerSpawner : IComponentData
{
    public Entity Player;
}

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
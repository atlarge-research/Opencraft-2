using Unity.Entities;
using UnityEngine;

public struct PlayerConfig : IComponentData
{
    public float Speed;
    public float JumpSpeed;
    public float Gravity;
}

public class PlayerConfigAuthoring : MonoBehaviour
{
    public float Speed = 5;
    public float JumpSpeed = 5;
    public float Gravity = 9.82f;
    class Baker : Baker<PlayerConfigAuthoring>
    {
        public override void Bake(PlayerConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new PlayerConfig{Speed = authoring.Speed, JumpSpeed = authoring.JumpSpeed, Gravity = authoring.Gravity});
        }
    }
}


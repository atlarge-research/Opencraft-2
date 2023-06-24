using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Opencraft.Networking.Authoring
{
    public class NetConfigAuthoring : MonoBehaviour
    {
        public int NetworkTickRate;
        public int SimulationTickRate;

        class Baker : Baker<NetConfigAuthoring>
        {
            public override void Bake(NetConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var clientServerTickRate = new ClientServerTickRate()
                {
                    NetworkTickRate = authoring.NetworkTickRate,
                    SimulationTickRate = authoring.SimulationTickRate
                };
                clientServerTickRate.ResolveDefaults();
                AddComponent(entity, clientServerTickRate);
            }
        }
    }
}


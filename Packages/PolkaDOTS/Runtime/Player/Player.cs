using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PolkaDOTS
{
    public struct Player : IComponentData
    {
        // Movement related fields
        [GhostField] public int JumpVelocity;
        [GhostField] public float Pitch;
        [GhostField] public float Yaw;
            
        // Connection related fields
        [GhostField] public FixedString32Bytes Username;
        public int OwnerNetworkId;
        public BlobAssetReference<BlobString> multiplayConnectionID;
    }
    
    // Marks this player entity as freshly instantiated
    public struct NewPlayer : IComponentData, IEnableableComponent
    {
    }
}
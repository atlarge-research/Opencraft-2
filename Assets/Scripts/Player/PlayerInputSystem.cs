using Opencraft.Player.Authoring;
using Opencraft.Player.Multiplay;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Unity.VisualScripting;


namespace Opencraft.Player
{
    // Collects player input from any local or guest clients every frame.
    // Also moves the camera locally for these clients
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public partial struct SamplePlayerInput : ISystem
    {

        private static float3 _cameraOffset = new(0, 2, -5);

        public void OnUpdate(ref SystemState state)
        {
            Multiplay.Multiplay multiplay = MultiplaySingleton.Instance;
            if (multiplay.IsUnityNull())
                return;
            // Apply movement input to owned player ghosts
            foreach (var (player, localToWorld, input)
                     in SystemAPI.Query<RefRO<Authoring.Player>, RefRO<LocalToWorld>, RefRW<PlayerInput>>()
                         .WithAll<GhostOwnerIsLocal>())
            {
                // Check if the connection has been created
                if (!player.ValueRO.multiplayConnectionID.IsCreated)
                    continue;
                
                ref var connID = ref player.ValueRO.multiplayConnectionID.Value;
                
                // Check if the connection has been terminated
                if (!multiplay.connectionPlayerObjects.ContainsKey(connID.ToString()))
                    continue;
                
                var playerObj = multiplay.connectionPlayerObjects[connID.ToString()];
                var playerController = playerObj.GetComponent<MultiplayPlayerController>();

                input.ValueRW.Movement = default;
                input.ValueRW.Jump = default;


                input.ValueRW.Movement.x = playerController.inputMovement.x;
                input.ValueRW.Movement.y = playerController.inputMovement.y;
                if (playerController.inputJump)
                {
                    input.ValueRW.Jump.Set();
                    playerController.inputJump = false;
                }
                
                input.ValueRW.Pitch = math.clamp(input.ValueRW.Pitch + playerController.inputLook.y, -math.PI / 2,
                    math.PI / 2);
                input.ValueRW.Yaw = math.fmod(input.ValueRW.Yaw + playerController.inputLook.x, 2 * math.PI);


                playerObj.transform.rotation = math.mul(quaternion.RotateY(input.ValueRO.Yaw),
                    quaternion.RotateX(-input.ValueRO.Pitch));
                var offset = math.rotate(playerObj.transform.rotation, _cameraOffset);
                playerObj.transform.position = localToWorld.ValueRO.Position + offset;
            }
        }
    }
}
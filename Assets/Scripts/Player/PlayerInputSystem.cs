using Opencraft.Player.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine;


namespace Opencraft.Player
{
    // Applies collected input to player entities
    // Also moves the camera locally for these clients
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public partial struct SamplePlayerInput : ISystem
    {
        private static float3 _cameraOffset = new float3(0.0f, Env.CAMERA_Y_OFFSET, 0.0f);

        public void OnCreate(ref SystemState state)
        {
            if (state.WorldUnmanaged.IsSimulatedClient())
                state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            PolkaDOTS.Multiplay.Multiplay multiplay = PolkaDOTS.Multiplay.MultiplaySingleton.Instance;
            if (multiplay.IsUnityNull())
                return;
            // Apply movement input to owned player ghosts
            foreach (var (player, localToWorld, input)
                     in SystemAPI.Query<RefRO<PlayerComponent>, RefRO<LocalToWorld>, RefRW<PlayerInput>>()
                         .WithAll<GhostOwnerIsLocal>())
            {
                // Check if the connection has been created
                if (!player.ValueRO.multiplayConnectionID.IsCreated)
                {
                    Debug.LogWarning($"Player {player.ValueRO.Username} has no connection ID!");
                    continue;
                }

                ref var connID = ref player.ValueRO.multiplayConnectionID.Value;

                // Check if the connection has been terminated
                if (!multiplay.connectionPlayerObjects.ContainsKey(connID.ToString()))
                {
                    Debug.LogWarning($"PlayerObject with connection ID {connID.ToString()} not found");
                    continue;
                }

                var playerObj = multiplay.connectionPlayerObjects[connID.ToString()];
                var playerController = playerObj.GetComponent<PolkaDOTS.Multiplay.MultiplayPlayerController>();


                input.ValueRW.Movement = default;
                input.ValueRW.Jump = default;
                input.ValueRW.PrimaryAction = default;
                input.ValueRW.SecondaryAction = default;
                input.ValueRW.ThirdAction = default;

                input.ValueRW.Movement.x = playerController.inputMovement.x;
                input.ValueRW.Movement.y = playerController.inputMovement.y;
                input.ValueRW.SelectedItem = playerController.selectableItems[playerController.selectedItemIndex];

                // Actions 
                if (playerController.inputJump)
                {
                    input.ValueRW.Jump.Set();
                    playerController.inputJump = false;
                }
                if (playerController.inputPrimaryAction)
                {
                    input.ValueRW.PrimaryAction.Set();
                    playerController.inputPrimaryAction = false;
                }
                if (playerController.inputSecondaryAction)
                {
                    input.ValueRW.SecondaryAction.Set();
                    playerController.inputSecondaryAction = false;
                }
                if (playerController.inputThirdAction)
                {
                    input.ValueRW.ThirdAction.Set();
                    playerController.inputThirdAction = false;
                }

                // Look
                input.ValueRW.Pitch = math.clamp(input.ValueRW.Pitch + playerController.inputLook.y, -math.PI / 2,
                    math.PI / 2);
                input.ValueRW.Yaw = math.fmod(input.ValueRW.Yaw + playerController.inputLook.x, 2 * math.PI);


                // Sync camera to look
                playerObj.transform.rotation = math.mul(quaternion.RotateY(input.ValueRO.Yaw),
                    quaternion.RotateX(-input.ValueRO.Pitch));
                //var offset = math.rotate(playerObj.transform.rotation, new float3(0)/*_cameraOffset*/);
                playerObj.transform.position = localToWorld.ValueRO.Position + _cameraOffset;

            }
        }
    }
}
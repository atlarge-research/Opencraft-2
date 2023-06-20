using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine;

// Collects player input every frame.
[UpdateInGroup(typeof(GhostInputSystemGroup))]
public partial struct SamplePlayerInput : ISystem
{
    
    public static readonly float3 k_CameraOffset = new float3(0, 2, -5);
    
    public void OnCreate(ref SystemState state)
    {
        
    }
    
    public void OnUpdate(ref SystemState state)
    {
        Multiplay multiplay = MultiplaySingleton.Instance;
        if (multiplay.IsUnityNull())
            return;
        // Apply movement input to owned player ghosts
        foreach (var (player,localToWorld, input)
                 in SystemAPI.Query<RefRO<Player>,RefRO<LocalToWorld>, RefRW<PlayerInput>>()
                     .WithAll<GhostOwnerIsLocal>())
        {
            if (!player.ValueRO.multiplayConnectionID.IsCreated)
            {
                continue;
            }
            ref var connID = ref player.ValueRO.multiplayConnectionID.Value;

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
                
            /*if (playerController.inputMovement.x < 0.0f)
                input.ValueRW.Movement.x -= 1;
            if (playerController.inputMovement.x > 0.0f)
                input.ValueRW.Movement.x += 1;
            if (playerController.inputMovement.y < 0.0f)
                input.ValueRW.Movement.y -= 1;
            if (playerController.inputMovement.y > 0.0f)
                input.ValueRW.Movement.y += 1;
            if (playerController.inputJump)
                input.ValueRW.Jump.Set();*/
            /*if (mouse1)
                input.ValueRW.PrimaryAction.Set();
            if (mouse2)
                input.ValueRW.SecondaryAction.Set();*/

            //float2 lookDelta = new float2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            input.ValueRW.Pitch = math.clamp(input.ValueRW.Pitch + playerController.inputLook.y, -math.PI/2, math.PI/2);
            input.ValueRW.Yaw = math.fmod(input.ValueRW.Yaw + playerController.inputLook.x, 2*math.PI);
            
            
            playerObj.transform.rotation = math.mul(quaternion.RotateY(input.ValueRO.Yaw), quaternion.RotateX(-input.ValueRO.Pitch));
            var offset = math.rotate(playerObj.transform.rotation, k_CameraOffset);
            playerObj.transform.position = localToWorld.ValueRO.Position + offset;
        }
    }
}
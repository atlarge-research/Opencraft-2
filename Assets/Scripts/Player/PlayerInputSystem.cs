using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

// Collects player input every frame. Also sends a spawn-player request RPC when player presses tab and no player 
// entity yet exists
[UpdateInGroup(typeof(GhostInputSystemGroup))]
public partial struct SamplePlayerInput : ISystem
{
    private bool _playerExists;

    public void OnCreate(ref SystemState state)
    {
        _playerExists = false;
    }
    
    public void OnUpdate(ref SystemState state)
    {
        bool tab = Input.GetKey("tab");
        // Request player to be spawned
        if (!_playerExists && tab)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (id, entity) in SystemAPI.Query<RefRO<NetworkId>>().WithEntityAccess().WithAll<NetworkStreamInGame>().WithNone<PlayerSpawned>())
            {
                commandBuffer.AddComponent<PlayerSpawned>(entity);
                var req = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<SpawnPlayerRequest>(req);
                Debug.Log("Sending spawn player RPC");
                commandBuffer.AddComponent(req, new SendRpcCommandRequest { TargetConnection = entity });
            }
            commandBuffer.Playback(state.EntityManager);
        }
        bool left = Input.GetKey("a");
        bool right = Input.GetKey("d");
        bool down = Input.GetKey("s");
        bool up = Input.GetKey("w");
        bool space = Input.GetKeyDown("space");
        bool mouse1 = Input.GetKeyDown(KeyCode.Mouse0);
        bool mouse2 = Input.GetKeyDown(KeyCode.Mouse1);

        // Apply movement input to an owned player ghost (there should only be one), if it exists
        foreach (var input in SystemAPI.Query<RefRW<PlayerInput>>().WithAll<GhostOwnerIsLocal>())
        {
            input.ValueRW.Movement = default;
            input.ValueRW.Jump = default;

            _playerExists = true;
            if (left)
                input.ValueRW.Movement.x -= 1;
            if (right)
                input.ValueRW.Movement.x += 1;
            if (down)
                input.ValueRW.Movement.y -= 1;
            if (up)
                input.ValueRW.Movement.y += 1;
            if (space)
                input.ValueRW.Jump.Set();
            if (mouse1)
                input.ValueRW.PrimaryAction.Set();
            if (mouse2)
                input.ValueRW.SecondaryAction.Set();
            
            float2 lookDelta = new float2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            input.ValueRW.Pitch = math.clamp(input.ValueRW.Pitch+lookDelta.y, -math.PI/2, math.PI/2);
            input.ValueRW.Yaw = math.fmod(input.ValueRW.Yaw + lookDelta.x, 2*math.PI);
        }
    }
}
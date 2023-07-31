using Opencraft.Player.Multiplay;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.VisualScripting;
using UnityEngine;

namespace Opencraft.Player.Emulated
{
    // ECS System wrapper around Multiplay class that handles render streaming connections
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class EmulationInitSystem : SystemBase
    {
        private EntityQuery connections;
        protected override void OnCreate()
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<NetworkId, NetworkStreamInGame>();
            connections = GetEntityQuery(builder);
        }

        protected override void OnUpdate()
        {
            // todo use coroutines
            Emulation emulation = EmulationSingleton.Instance;
            Multiplay.Multiplay multiplay = MultiplaySingleton.Instance;
            if (emulation.IsUnityNull() || multiplay.IsUnityNull())
                return;
            // Wait for either clientworld to be connected to the server, or for this guest client to be connected
            if (CmdArgs.ClientStreamingRole == CmdArgs.StreamingRole.Guest)
            {
                if(!multiplay.IsGuestConnected())
                    return;
            }
            else
            {
                if(connections.IsEmpty)
                    return;
            }
            Enabled = false;

            emulation.emulationType = CmdArgs.emulationType;
            Debug.Log($"Emulation type is {emulation.emulationType}");
            
            // Multiplay guest emulation only supports input playback
            if (CmdArgs.ClientStreamingRole == CmdArgs.StreamingRole.Guest && emulation.emulationType == EmulationType.BehaviourProfile)
            {
                Debug.LogWarning("Multiplay guest emulation only supports input playback, switching to it.");
                emulation.emulationType = EmulationType.InputPlayback;
            }

            switch (emulation.emulationType)
            {
                case EmulationType.RecordInput:
                    emulation.initializeRecording();
                    break;
                case EmulationType.InputPlayback:
                    emulation.initializePlayback();
                    break;
                case EmulationType.BehaviourProfile:
                case EmulationType.None:
                default:
                    break;
            }

        }

    }
}
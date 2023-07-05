using Unity.Entities;
using Unity.VisualScripting;
using UnityEngine;

namespace Opencraft.Player.Emulated
{
    // ECS System wrapper around Multiplay class that handles render streaming connections
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class EmulationInitSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Emulation emulation = EmulationSingleton.Instance;
            if (emulation.IsUnityNull())
                return;
            Enabled = false;
            
            emulation.emulationType = CmdArgs.emulationType;
            
            // Multiplay guest emulation only supports input playback
            if (CmdArgs.ClientStreamingRole == CmdArgs.StreamingRole.Guest && emulation.emulationType != EmulationType.InputPlayback)
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
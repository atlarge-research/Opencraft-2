using System;
using Opencraft.Deployment;
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
            if (Config.multiplayStreamingRoles == MultiplayStreamingRoles.Guest)
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

            emulation.emulationType = Config.EmulationType;
            Debug.Log($"Emulation type is {emulation.emulationType}");
            
            // Multiplay guest emulation only supports input playback
            if (Config.multiplayStreamingRoles == MultiplayStreamingRoles.Guest && (emulation.emulationType & EmulationBehaviours.Simulation) == EmulationBehaviours.Simulation)
            {
                Debug.LogWarning("Multiplay guest emulation only supports input playback, switching to it.");
                emulation.emulationType ^= EmulationBehaviours.Simulation;
                emulation.emulationType |= EmulationBehaviours.Playback;
            }
            
            if((emulation.emulationType & EmulationBehaviours.Record) == EmulationBehaviours.Record)
                emulation.initializeRecording();
            if((emulation.emulationType & EmulationBehaviours.Playback) == EmulationBehaviours.Playback)
                emulation.initializePlayback();
            if ((emulation.emulationType & EmulationBehaviours.Simulation) == EmulationBehaviours.Simulation)
                throw new NotImplementedException(); // TODO
        }
    }
}
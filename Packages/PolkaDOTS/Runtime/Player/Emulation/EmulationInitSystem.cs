using System;
using PolkaDOTS.Deployment;
using PolkaDOTS.Multiplay;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.VisualScripting;
using UnityEngine;

namespace PolkaDOTS.Emulation
{
    /// <summary>
    ///  Sets up and starts emulated player input 
    /// </summary>
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
            if (emulation is null || multiplay is null)
                return;
            // Wait for either clientworld to be connected to the server, or for a guest client to be connected
            if (!(multiplay.IsGuestConnected() || !connections.IsEmpty))
            {
                return;
            }
            Enabled = false;

            emulation.emulationType = Config.EmulationType;
            Debug.Log($"Emulation type is {emulation.emulationType}");
            
            // Multiplay guest emulation only supports input playback
            if (Config.multiplayStreamingRoles == MultiplayStreamingRoles.Guest && (emulation.emulationType & EmulationBehaviours.Simulation) == EmulationBehaviours.Simulation)
            {
                Debug.Log("Multiplay guest emulation only supports input playback, switching to it.");
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
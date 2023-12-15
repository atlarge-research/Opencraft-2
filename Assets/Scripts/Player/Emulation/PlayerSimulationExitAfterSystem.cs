using System;
using PolkaDOTS;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Opencraft.Player.Emulation
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct PlayerSimulationExitAfterSystem: ISystem
    {
        private int _duration;
        private double _startTime;
        public void OnCreate(ref SystemState state)
        {
            if (!state.WorldUnmanaged.IsSimulatedClient())
            {
                state.Enabled = false;
                return;
            }
            if (ApplicationConfig.Duration > 0)
            {
                _duration = ApplicationConfig.Duration;
                _startTime = Double.NaN;
            }
            else
            {
                state.Enabled = false;
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (double.IsNaN(_startTime))
            {
                _startTime = state.WorldUnmanaged.Time.ElapsedTime;
                return;
            }

            if (state.WorldUnmanaged.Time.ElapsedTime - _startTime > _duration)
            {
                Debug.Log($"[{DateTime.Now.TimeOfDay}]: Experiment duration of {_duration} seconds elapsed! Stopping {state.WorldUnmanaged.Name}");
                Entity exitReq = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(exitReq, new ExitWorld());
                state.Enabled = false;
            }
            
        }
    }
}
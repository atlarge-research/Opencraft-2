using System;
using Opencraft.Bootstrap;
using Opencraft.Statistics;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Opencraft
{
    /// <summary>
    /// Run in all worlds to exit after after Config.Duration seconds
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)] 
    //[UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial class ExitAfterDurationSystem : SystemBase
    {
        protected override void OnCreate()
        {
            if (Config.Duration <= 0)
                Enabled = false;
        }

        /// <summary>
        /// When time elapsed, mark this world as stopped. Depending on mode the game will exit or this world will be removed by <see cref="WorldHeartbeatMonitor"/>
        /// </summary>
        protected override void OnUpdate()
        {
            if ((World.IsClient() || World.IsServer()) && (Config.SwitchToStreamDuration > 0) &&
                (World.Time.ElapsedTime >= Config.SwitchToStreamDuration))
            {
                World.QuitUpdate = true;
            }
            if (World.Time.ElapsedTime >= Config.Duration)
            {
                Debug.Log($"[{DateTime.Now.TimeOfDay}]: Experiment duration of {Config.Duration} seconds elapsed! Exiting.");
                
                StatisticsToFile.instance.WriteStatisticsBuffer();
                
                #if UNITY_EDITOR
                EditorApplication.ExitPlaymode();
                #else
                Application.Quit();
                #endif
                
            }
        }
    }
}
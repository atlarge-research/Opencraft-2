using System;
using Unity.Entities;
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
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial class ExitAfterDurationSystem : SystemBase
    {
        protected override void OnCreate()
        {
            if (Config.Duration <= 0)
                Enabled = false;
        }

        protected override void OnUpdate()
        {
            if (World.Time.ElapsedTime >= Config.Duration)
            {
                Debug.Log($"[{DateTime.Now.TimeOfDay}]: Experiment duration of {Config.Duration} seconds elapsed! Exiting.");
                
                //World.DisposeAllWorlds();
                #if UNITY_EDITOR
                EditorApplication.ExitPlaymode();
                #else
                Application.Quit();
                #endif
                
            }
        }
    }
}
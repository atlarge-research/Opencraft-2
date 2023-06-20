using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.RenderStreaming;
using Unity.VisualScripting;
using UnityEngine;

// ECS System wrapper around Multiplay class that handles render streaming connections
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class MultiplayInitSystem: SystemBase
{
    protected override void OnUpdate()
    {
        // Begin multiplay hosts in OnUpdate to ensure the GameObjects it references are properly initialized
        Multiplay multiplay = MultiplaySingleton.Instance;
        if (multiplay.IsUnityNull())
            return;
        // No need to run this  more than once
        Enabled = false;
        
        switch (CmdArgs.ClientStreamingRole)
        {
            case CmdArgs.StreamingRole.Disabled:
                Debug.Log("Multiplay is disabled.");
                multiplay.SetUpLocalPlayer();
                break;
            case CmdArgs.StreamingRole.Host:
                Debug.Log("Setting up multiplay host!");
                multiplay.SetUpHost();
                break;
            case CmdArgs.StreamingRole.Guest:
                Debug.Log("Setting up multiplay guest!");
                multiplay.SetUpGuest();
                break;
        }
    }
    
}
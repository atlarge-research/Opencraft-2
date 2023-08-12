using Unity.Entities;
using Unity.VisualScripting;
using UnityEngine;

namespace Opencraft.Player.Multiplay
{
    // ECS System wrapper around Multiplay class that handles render streaming connections
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class MultiplayInitSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Begin multiplay hosts in OnUpdate to ensure the GameObjects it references are properly initialized
            Multiplay multiplay = MultiplaySingleton.Instance;
            if (multiplay.IsUnityNull())
                return;
            // No need to run this  more than once
            Enabled = false;

            switch (Config.multiplayStreamingRoles)
            {
                case MultiplayStreamingRoles.Disabled:
                    Debug.Log("Multiplay is disabled.");
                    multiplay.SetUpLocalPlayer();
                    break;
                case MultiplayStreamingRoles.Host:
                    Debug.Log("Setting up multiplay host!");
                    multiplay.SetUpHost();
                    break;
                case MultiplayStreamingRoles.Guest:
                    Debug.Log("Setting up multiplay guest!");
                    multiplay.SetUpGuest();
                    break;
            }
        }

    }
}
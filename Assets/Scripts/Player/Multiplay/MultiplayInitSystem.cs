using Opencraft.Deployment;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.VisualScripting;
using UnityEngine;

namespace Opencraft.Player.Multiplay
{
    // ECS System wrapper around Multiplay class that handles render streaming connections
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class MultiplayInitSystem : SystemBase
    {
        private EntityQuery _requestQuery;
        protected override void OnCreate()
        {
            _requestQuery = GetEntityQuery(ComponentType.ReadOnly<NetworkStreamRequestConnect>());
        }
        
        protected override void OnUpdate()
        {

            // Begin multiplay hosts in OnUpdate to ensure the GameObjects it references are properly initialized
            Multiplay multiplay = MultiplaySingleton.Instance;
            if (multiplay.IsUnityNull())
                return;
            multiplay.InitSettings();

            if (World.Unmanaged.IsStreamedClient())
            {
                // Check for connection requests
                if (!_requestQuery.IsEmpty)
                {
                    var requests = _requestQuery.ToComponentDataArray<NetworkStreamRequestConnect>(Allocator.Temp);
                    if(requests.Length > 1)
                        Debug.Log($"Render streaming guest has multiple connection requests! Ignoring all but the first...");
                    NetworkEndpoint endpoint = requests[0].Endpoint;
                    Debug.Log($"Got multiplay guest endpoint of {endpoint}");
                    multiplay.SetUpGuest(endpoint);
                    EntityManager.DestroyEntity(_requestQuery);
                }
                // Return regardless of connections
                return;
            }
            else if (World.Unmanaged.IsHostClient())
            {
                multiplay.SetUpHost();
            }
            else
            {
                Debug.Log("Multiplay is disabled.");
                multiplay.SetUpLocalPlayer();
            }
            // Only need to run once if Multiplay disabled or on a host 
            Enabled = false;
            
        }

    }
}
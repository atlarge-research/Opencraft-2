using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Scenes;
using UnityEngine;

namespace Opencraft
{
    // Runtime component, SceneSystem uses EntitySceneReference to identify scenes.
    public struct AuthoringSceneReference : IComponentData
    {
        public EntitySceneReference SceneReference;
    }
    
    public struct LoadAuthoringSceneRequest : IComponentData
    {
        public WorldUnmanaged world;
    }

#if UNITY_EDITOR
// Authoring component, a SceneAsset can only be used in the Editor
    public class SceneLoader : MonoBehaviour
    {
        public UnityEditor.SceneAsset Scene;

        class Baker : Baker<SceneLoader>
        {
            public override void Bake(SceneLoader authoring)
            {
                
                Debug.Log($"Run authoring scene baking!");
                var reference = new EntitySceneReference(authoring.Scene);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new AuthoringSceneReference()
                {
                    SceneReference = reference
                });
            }
        }
    }
#endif
    
    /// <summary>
    /// Run in Deployment world to call scene loads on created worlds. Fix for inconsistent authoring scene loading.
    /// There is likely a better method.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Disabled)] // Don't automatically add to any worlds
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial class AuthoringSceneLoaderSystem : SystemBase
    {
        private EntityQuery newRequests;
        private EntityQuery authoringSceneQuery;

        protected override void OnCreate()
        {
            newRequests = GetEntityQuery(typeof(LoadAuthoringSceneRequest));
            authoringSceneQuery = GetEntityQuery(typeof(AuthoringSceneReference));
        }

        protected override void OnUpdate()
        {
            var requests = newRequests.ToComponentDataArray<LoadAuthoringSceneRequest >(Allocator.Temp);
            var authoringScenes = authoringSceneQuery.ToComponentDataArray<AuthoringSceneReference>(Allocator.Temp);

            // Can't use a foreach with a query as SceneSystem.LoadSceneAsync does structural changes
            for (int i = 0; i < requests.Length; i += 1)
            {
                WorldUnmanaged world = requests[i].world;
                Debug.Log($"Loading authoring scene in world {world.Name}");
                SceneSystem.LoadSceneAsync(world, authoringScenes[0].SceneReference);
            }

            requests.Dispose();
            authoringScenes.Dispose();
            EntityManager.DestroyEntity(newRequests);
        }
    }
    
    /// <summary>
    /// Run in all worlds to spawn the authoring scene within this world, without needing the deployment service.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)] // Don't automatically add to any worlds
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial class AutoAuthoringSceneLoaderSystem : SystemBase
    {
        private EntityQuery authoringSceneQuery;

        protected override void OnCreate()
        {
            authoringSceneQuery = GetEntityQuery(typeof(AuthoringSceneReference));
        }

        protected override void OnUpdate()
        {
            var authoringScenes = authoringSceneQuery.ToComponentDataArray<AuthoringSceneReference>(Allocator.Temp);
            Debug.Log($"AutoLoading authoring scene in world {World.Unmanaged.Name}");
            SceneSystem.LoadSceneAsync(World.Unmanaged, authoringScenes[0].SceneReference);
            
            authoringScenes.Dispose();
            EntityManager.DestroyEntity(authoringSceneQuery);
        }
    }

}
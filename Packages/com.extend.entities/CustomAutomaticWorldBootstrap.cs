using UnityEngine;

namespace Unity.Entities
{
    /// <summary>
    /// Calls CustomWorldInitialization on game start, before a scene completes loading. Replaces by <see cref="AutomaticWorldBootstrap"/>
    /// </summary>
    static class CustomAutomaticWorldBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            CustomWorldInitialization.Initialize("Default World", false);
        }
    }

}
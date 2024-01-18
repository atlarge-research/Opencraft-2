using UnityEngine;

namespace Opencraft.Terrain
{
    /// MonoBehaviour wrapper to make the <see cref="TerrainBank"/> singleton available
    public class TerrainBankSingleton : MonoBehaviour
    {
        public static TerrainBank Instance;

        void Awake()
        {
            Instance = GetComponent<TerrainBank>();
        }

        public static void Destroy()
        {
            Destroy(Instance);
        }
    }
}
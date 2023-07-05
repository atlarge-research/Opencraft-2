using UnityEngine;

namespace Opencraft.Player.Emulated
{
    // MonoBehaviour wrapper to make the Multiplay singleton available
    public class EmulationSingleton : MonoBehaviour
    {
        public static Emulation Instance;

        void Awake()
        {
            Instance = GetComponent<Emulation>();
        }

        public static void Destroy()
        {
            Destroy(Instance);
        }
    }
}
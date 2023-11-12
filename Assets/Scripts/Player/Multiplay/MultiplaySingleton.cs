using UnityEngine;

namespace Opencraft.Player.Multiplay
{
    // MonoBehaviour wrapper to make the Multiplay singleton available
    public class MultiplaySingleton : MonoBehaviour
    {
        public static Multiplay Instance;

        void Awake()
        {
            Debug.Log("Initializing multiplay!");
            Instance = GetComponent<Multiplay>();
            Instance.InitSettings();
        }

        public static void Destroy()
        {
            Destroy(Instance);
        }
    }
}
using UnityEngine;

public class MultiplaySingleton : MonoBehaviour
{
    public static Multiplay Instance;

    void Awake()
    {
       Instance = GetComponent<Multiplay>();
    }

    public static void Destroy()
    {
        Destroy(Instance);
    }
}
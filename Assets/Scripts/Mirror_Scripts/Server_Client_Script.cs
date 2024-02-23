using UnityEngine;
using Mirror;

public class NetworkHelper : MonoBehaviour
{
    public NetworkManager networkManager;
    public bool startServerInEditor = false;

    // Start is called before the first frame update
    void Start()
    {
        if (networkManager == null)
        {
            Debug.LogError("Network Manager is not assigned!");
            return;
        }

#if UNITY_EDITOR
        if (startServerInEditor)
        {
            networkManager.StartServer();
        }
        else
        {
            networkManager.StartClient();
        }
#else
        string[] args = System.Environment.GetCommandLineArgs();

        foreach (string arg in args)
        {
            if (arg == "-server")
            {
                networkManager.StartServer();
                return;
            }
            else if (arg == "-client")
            {
                networkManager.StartClient();
                return;
            }
        }

        Debug.LogError("No valid command-line arguments provided.");
#endif
    }
}
using UnityEngine;
using Mirror;

public class NetworkHelper : MonoBehaviour
{
    public NetworkManager networkManager;
    public bool startServer = false;

    // Start is called before the first frame update
    void Start()
    {
        if (networkManager == null)
        {
            Debug.LogError("Network Manager is not assigned!");
            return;
        }

        if (startServer)
        {
            networkManager.StartServer();
        }
        else
        {
            networkManager.StartClient();
        }
    }
}
// using UnityEngine;
// using Mirror;
//
// public class FlatMapGenerator : NetworkBehaviour
// {
//     public GameObject blockPrefab;
//     public int mapWidth;
//     public int mapLength;
//     public int mapHeight;
//
//     void Start()
//     {
//         if (isServer)
//         {
//             GenerateFlatMap();
//         }
//     }
//
//     void GenerateFlatMap()
//     {
//         if (blockPrefab == null)
//         {
//             Debug.LogError("Block prefab is not assigned.");
//             return;
//         }
//
//         Vector3 blockDimensions = blockPrefab.GetComponent<Renderer>().bounds.size;
//
//         float xOffset = mapWidth * blockDimensions.x / 2f;
//         float zOffset = mapLength * blockDimensions.z / 2f;
//
//         // float playerSpawnX = mapWidth * blockDimensions.x / 2f;
//         // float playerSpawnZ = mapLength * blockDimensions.z / 2f;
//         // Vector3 playerSpawnPosition = new Vector3(playerSpawnX, mapHeight * blockDimensions.y, playerSpawnZ);
//
//         for (int x = 0; x < mapWidth; x++)
//         {
//             for (int z = 0; z < mapLength; z++)
//             {
//                 for (int y = 0; y < mapHeight; y++)
//                 {
//                     Vector3 position = new Vector3(x * blockDimensions.x - xOffset, y * blockDimensions.y, z * blockDimensions.z - zOffset);
//                     GameObject block = Instantiate(blockPrefab, position, Quaternion.identity);
//
//                     NetworkServer.Spawn(block);
//                 }
//             }
//         }
//
//         // GameObject player = GameObject.FindWithTag("Player");
//         // if (player != null)
//         // {
//         //     player.transform.position = playerSpawnPosition;
//         // }
//     }
// }

using UnityEngine;
using Mirror;

public class FlatMapGenerator : NetworkBehaviour
{
    public GameObject blockPrefab;
    public int mapWidth;
    public int mapLength;
    public int mapHeight;

    void Start()
    {
        if (isServer)
        {
            GenerateFlatMap();
        }
    }

    void GenerateFlatMap()
    {
        if (blockPrefab == null)
        {
            Debug.LogError("Block prefab is not assigned.");
            return;
        }

        Vector3 blockDimensions = blockPrefab.GetComponent<Renderer>().bounds.size;

        float xOffset = mapWidth * blockDimensions.x / 2f;
        float zOffset = mapLength * blockDimensions.z / 2f;

        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapLength; z++)
            {
                for (int y = 0; y < mapHeight; y++)
                {
                    Vector3 position = new Vector3(x * blockDimensions.x - xOffset, y * blockDimensions.y, z * blockDimensions.z - zOffset);
                    GameObject block = Instantiate(blockPrefab, position, Quaternion.identity);

                    NetworkServer.Spawn(block);
                }
            }
        }
    }
}

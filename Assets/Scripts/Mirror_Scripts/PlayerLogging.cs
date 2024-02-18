using System;
using UnityEngine;
using Mirror;
using System.IO;

public class PlayerLogging : NetworkBehaviour
{
    private string logFileName;
    private StreamWriter writer;

    private bool isLoggingInitialized = false;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        if (!isLoggingInitialized)
        {
            InitializeLogging();
            isLoggingInitialized = true;
        }
    }

    void InitializeLogging()
    {
        string connectionId = Guid.NewGuid().ToString(); // Generate a unique identifier for each client
        logFileName = "player_log_" + connectionId + ".txt";
        string path = Path.Combine(Application.persistentDataPath, logFileName);
        writer = new StreamWriter(path, true);

        LogPlayerData();
    }

    void LogPlayerData()
    {
        InvokeRepeating(nameof(LogPosition), 0f, 1f);
    }

    void LogPosition()
    {
        if (transform == null)
            return;

        Vector3 playerPosition = transform.position;
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        writer.WriteLine($"[{timestamp}] Player Position: {playerPosition}");
        writer.Flush(); 
    }

    void OnDestroy()
    {
        if (writer != null)
        {
            writer.Close();
        }
    }
}
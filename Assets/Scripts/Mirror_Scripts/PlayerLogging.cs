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
        string connectionId = Guid.NewGuid().ToString();
        logFileName = "player_log_" + connectionId + ".txt";
        
        string logDirectory = Path.Combine(Application.dataPath, "mirror_logs");

        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        string path = Path.Combine(logDirectory, logFileName);
        writer = new StreamWriter(path, true);

        LogPlayerData();
    }

    void LogPlayerData()
    {
        InvokeRepeating(nameof(LogPlayerState), 0f, 1f);
    }

    void LogPlayerState()
    {
        if (transform == null)
            return;

        Vector3 playerPosition = transform.position;
        double player_rtt = NetworkTime.rtt * 1000;
        string formattedLatency = player_rtt.ToString("F2");
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        writer.WriteLine($"[{timestamp}] Player Position: {playerPosition} Round-trip delay: {formattedLatency} ms");

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
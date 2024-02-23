using UnityEngine;
using Mirror;
using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading;

public class ServerMetricsLogger : NetworkBehaviour
{
    private string logFileName;
    private StreamWriter writer;

    private bool isLoggingInitialized = false;

    private DateTime serverStartTime;

    public override void OnStartServer()
    {
        base.OnStartServer();

        serverStartTime = DateTime.Now;

        if (!isLoggingInitialized)
        {
            InitializeLogging();
            isLoggingInitialized = true;
        }
    }

    void InitializeLogging()
    {
        logFileName = "server_log.txt";
        string logDirectory = Path.Combine(Application.dataPath, "mirror_logs");
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }
        string path = Path.Combine(logDirectory, logFileName);
        writer = new StreamWriter(path, true);

        LogServerMetrics();
    }

    void LogServerMetrics()
    {
        InvokeRepeating(nameof(LogMetrics), 0f, 60f); // Log metrics every minute
    }

    void LogMetrics()
    {
        // Calculate server uptime
        TimeSpan uptime = DateTime.Now - serverStartTime;

        // Get the count of connected players
        int playerCount = NetworkServer.connections.Count;

        // Get the count of objects on the server (excluding players)
        int objectCount = FindObjectsOfType<NetworkIdentity>().Count() - playerCount;

        // Log server metrics to file
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        writer.WriteLine($"[{timestamp}] Server Uptime: {uptime}, Connected Players: {playerCount}, Objects on Server: {objectCount}");

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

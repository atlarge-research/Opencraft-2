﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using Unity.VisualScripting;
using UnityEngine;


namespace Opencraft.Statistics
{
    /// <summary>
    /// Writes key statistics to a csv file
    /// </summary>
    // todo this is a (suboptimal) workaround for inability to properly convert profiler .raw files to csv.
    public class StatisticsWriterInstance : MonoBehaviour
    {
        public static StatisticsWriter instance;
        public static bool ready;
        private void Awake()
        {
            if (!Config.LogStats) {
                return;
            }

            if (File.Exists(Config.StatsFilePath)){
                // Don't overwrite existing data
                Debug.Log($"Stats file {Config.StatsFilePath} already exists. Ignoring -logStats.");
                return;
            }

           
            Debug.Log("Creating statistics writer!");
            instance = new StatisticsWriter();
            ready = true;
            
        }

        private void OnDisable()
        {
            if (Config.LogStats && ready)
            {
                // Write statistics before exit
                if(!instance.written)
                    instance.WriteStatisticsBuffer();
            }

            ready = false;
        }

        public void Update()
        {
            if (Config.LogStats && ready)
            {
                instance.Update();
            }
        }

        public static void WriteStatisticsBuffer()
        {
            if (!instance.IsUnityNull())
            {
                instance.WriteStatisticsBuffer();
            }
        }
        
    }

    public class StatisticsWriter
    {
        private Dictionary<string, ProfilerRecorder> recorders;
        
        private string metricsBuffer;
        
        public bool written;
        
        public StatisticsWriter()
        {
            recorders = new Dictionary<string, ProfilerRecorder>();
            written = false;
            
            recorders.Add("Main Thread", ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread"));
            recorders.Add("System Used Memory", ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory"));
            recorders.Add("GC Reserved Memory", ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory"));
            recorders.Add("Total Reserved Memory", ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory"));
            recorders.Add("Number of Terrain Areas (Client)", ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Number of Terrain Areas (Client)"));
            recorders.Add("Number of Terrain Areas (Server)", ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Number of Terrain Areas (Server)"));
            recorders.Add("NFE Snapshot Tick", ProfilerRecorder.StartNew(ProfilerCategory.Network, "NFE Snapshot Tick")); // Can be used to sync logs
            recorders.Add("NFE Snapshot Size (bits)", ProfilerRecorder.StartNew(ProfilerCategory.Network, "NFE Snapshot Size (bits)"));
            recorders.Add("NFE RTT", ProfilerRecorder.StartNew(ProfilerCategory.Network, "NFE RTT"));
            recorders.Add("NFE Jitter", ProfilerRecorder.StartNew(ProfilerCategory.Network, "NFE Jitter"));
            recorders.Add("Multiplay FPS", ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Multiplay FPS"));
            recorders.Add("Multiplay BitRate In", ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Multiplay BitRate In"));
            recorders.Add("Multiplay BitRate Out", ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Multiplay BitRate Out"));
            recorders.Add("Multiplay RTT (ms)", ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Multiplay RTT (ms)"));
            recorders.Add("Multiplay PacketLoss", ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Multiplay PacketLoss"));

            /*foreach (var (name, recorder) in recorders)
            {
                if (!recorder.Valid)
                {
                    Debug.LogWarning($"Recorder [{name}] is invalid!");
                }
            }*/
        }

        private byte[] HeaderToBytes()
        {
            var sb = new StringBuilder("Frame Number;");
            foreach (var (name, _) in recorders)
                sb.Append($"{name};");
            sb.Append("\n");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }
        
        public void Update()
        {
            var sb = new StringBuilder($"{Time.frameCount};");
            foreach (var (_, rec) in recorders)
                sb.Append($"{rec.LastValue.ToString()};");
            sb.Append("\n");
            metricsBuffer += sb.ToString();

        }

        public void WriteStatisticsBuffer()
        {
            if (written)
            {
                Debug.LogWarning("Already wrote stats to file!");
                return;
            }

            Debug.Log("Writing stats to file");
            try
            {
                // Write header
                if (!File.Exists(Config.StatsFilePath))
                {
                    using (var file = File.Open(Config.StatsFilePath, FileMode.Create))
                    {
                        file.Write(HeaderToBytes());
                    }
                }
                
                // Write data
                using (var file = File.Open(Config.StatsFilePath, FileMode.Append))
                {
                    file.Write(Encoding.ASCII.GetBytes(metricsBuffer));
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to write statistics to {Config.StatsFilePath} with exception {e}");
            }
            
            written = true;
        }
        
    }
    

}
using System;
using UnityEngine;
using UnityEngine.Profiling;
using WebSocketSharp;

namespace Opencraft.Statistics
{
    /// <summary>
    /// Sets new profiler .raw log files every NUM_FRAMES.
    /// </summary>
    public class RuntimeProfilerManager : MonoBehaviour
    {
        private string baseFile;
        private int fileOffset = 1;
        private int lastUpdate = 0;
        private int NUM_FRAMES = 1750;
        private void Awake()
        {
            if (!Profiler.enabled || Profiler.logFile.IsNullOrEmpty())
            {
                this.enabled = false;
                return;
            }
            // Filename without .raw extension
            baseFile = Profiler.logFile.Substring(0,  Profiler.logFile.Length-4);
            
        }

        private void Update()
        {
            int currentFrame = Time.frameCount;
            if (currentFrame - lastUpdate >= NUM_FRAMES)
            {
                NewProfilerLogFile();
                lastUpdate = currentFrame;
            }
        }

        private void NewProfilerLogFile()
        {
            Profiler.logFile = $"{baseFile}_{fileOffset}.raw";
            fileOffset++;
        }
    }
}
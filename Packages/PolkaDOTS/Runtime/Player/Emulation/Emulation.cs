using System;
using Unity.Serialization;
using UnityEngine;

namespace PolkaDOTS.Emulation
{
    /// <summary>
    /// Runs player emulation
    /// </summary>
    public class Emulation : MonoBehaviour
    {
        public InputRecorder inputRecorder;
        [DontSerialize]public EmulationBehaviours emulationType = EmulationBehaviours.None;

        public void Pause()
        {
            if ((emulationType & EmulationBehaviours.Playback) == EmulationBehaviours.Playback)
            {
                inputRecorder.PauseReplay();
            }
        }

        public void Play()
        {
            if ((emulationType & EmulationBehaviours.Playback) == EmulationBehaviours.Playback)
            {
                inputRecorder.StartReplay();
            }
        }
        

        public void initializePlayback()
        {
            if (inputRecorder.replayIsRunning)
            {
                Debug.Log($"Resuming input playback from {Config.EmulationFilePath}");
                Play();
                return;
            }
            try
            {
                inputRecorder.LoadCaptureFromFile(Config.EmulationFilePath);
            }
            catch (Exception ex)
            {
                Debug.Log("Failed to load input playback file with error:");
                Debug.LogError(ex.ToString());
                return;
            } 
            Debug.Log($"Starting input playback from {Config.EmulationFilePath}");
            inputRecorder.StartReplay();
        }
        
        public void initializeRecording()
        {
            Debug.Log("Starting input capturing!");
            //inputRecorder.gameObject.SetActive(true);
            inputRecorder.StartCapture();
        }
        
        private void OnApplicationQuit()
        {
            if ((emulationType & EmulationBehaviours.Record) == EmulationBehaviours.Record)
            {
                Debug.Log($"Saving capture file to {Config.EmulationFilePath}");
                inputRecorder.StopCapture();
                inputRecorder.SaveCaptureToFile(Config.EmulationFilePath);
            }

            if ((emulationType & EmulationBehaviours.Playback) == EmulationBehaviours.Playback)
            {
                inputRecorder.StopReplay();
            }
        }
    }
    
}
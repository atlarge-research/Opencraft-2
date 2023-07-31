using System;
using System.IO;
using Opencraft.Player.Emulated.InputPlayback;
using UnityEngine;

namespace Opencraft.Player.Emulated
{
    public class Emulation : MonoBehaviour
    {
        public string filename = "recordedInputs.inputtrace";
        public InputRecorder inputRecorder;
        public EmulationType emulationType = EmulationType.None;
        
        
        // Behaviour script
        // InputPlayback file
        // RecordInput file. Use run-length encoding
        
        //path = Application.persistentDataPath + inputFile;

        public void initializePlayback()
        {
            try
            {
                inputRecorder.LoadCaptureFromFile(CmdArgs.EmulationFile);
            }
            catch (Exception ex)
            {
                Debug.Log("Failed to load input playback file with error:");
                Debug.LogError(ex);
                return;
            } 
            Debug.Log($"Starting input playback from {CmdArgs.EmulationFile}");
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
            if (emulationType == EmulationType.RecordInput)
            {
                Debug.Log($"Saving capture file to {Application.persistentDataPath + '/' +filename}");
                inputRecorder.StopCapture();
                inputRecorder.SaveCaptureToFile(Application.persistentDataPath + '/' + filename);
            }

            if (emulationType == EmulationType.InputPlayback)
            {
                inputRecorder.StopReplay();
            }
        }
    }

    public enum EmulationType
    {
        None,
        InputPlayback,
        RecordInput,
        BehaviourProfile
    }
}
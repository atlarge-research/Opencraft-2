using System;
using System.IO;
using Opencraft.Player.Emulated.InputPlayback;
using Unity.Serialization;
using UnityEngine;

namespace Opencraft.Player.Emulated
{
    public class Emulation : MonoBehaviour
    {
        public string filename = "recordedInputs.inputtrace";
        public InputRecorder inputRecorder;
        [DontSerialize]public EmulationBehaviours emulationType = EmulationBehaviours.None;
        
        
        // Behaviour script
        // InputPlayback file
        // RecordInput file. Use run-length encoding
        
        //path = Application.persistentDataPath + inputFile;

        public void initializePlayback()
        {
            try
            {
                inputRecorder.LoadCaptureFromFile(Config.EmulationFilePath);
            }
            catch (Exception ex)
            {
                Debug.Log("Failed to load input playback file with error:");
                Debug.LogError(ex);
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
                Debug.Log($"Saving capture file to {Application.persistentDataPath + '/' +filename}");
                inputRecorder.StopCapture();
                inputRecorder.SaveCaptureToFile(Application.persistentDataPath + '/' + filename);
            }

            if ((emulationType & EmulationBehaviours.Playback) == EmulationBehaviours.Playback)
            {
                inputRecorder.StopReplay();
            }
        }
    }

    /*public enum EmulationType
    {
        None,
        InputPlayback,
        RecordInput,
        BehaviourProfile
    }*/
}
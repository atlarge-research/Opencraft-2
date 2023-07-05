using System;
using Opencraft.Player.Emulated.InputPlayback;
using UnityEngine;

namespace Opencraft.Player.Emulated
{
    public class Emulation : MonoBehaviour
    {
        public string inputFile = "recordedInputs.inputtrace";
        public InputRecorder inputRecorder;
        public EmulationType emulationType = EmulationType.None;
        
        
        // Behaviour script
        // InputPlayback file
        // RecordInput file. Use run-length encoding
        
        //path = Application.persistentDataPath + inputFile;

        public void initializePlayback()
        {
            Debug.Log("Starting input playback!");
            //inputRecorder.gameObject.SetActive(true);
            inputRecorder.LoadCaptureFromFile(Application.persistentDataPath + '/' +inputFile);
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
                Debug.Log("Saving capture file!");
                inputRecorder.StopCapture();
                inputRecorder.SaveCaptureToFile(Application.persistentDataPath + '/' + inputFile);
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
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Performance.ProfileAnalyzer;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;


namespace Editor
{
    [EditorWindowTitle(title = "ProfilerUtility")]
    public class ProfilerUtilityWindow : EditorWindow
    {
        private string _currentFile;
        private int _currentIndex;
        
        private static readonly string[] LoadProfilingDataFileFilters = new string[4]
        {
            L10n.Tr("Profiler files"),
            "data,raw",
            L10n.Tr("All files"),
            "*"
        };
        
        [MenuItem("Window/Analysis/ProfilerUtility", false, 0)]
        internal static ProfilerUtilityWindow ShowProfilerUtilityWindow() => EditorWindow.GetWindow<ProfilerUtilityWindow>(false);

        internal ProfilerUtilityWindow()
        {
        }
        
        void OnGUI()
        {
            //GUILayout.Label("Profiler .raw file", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Profiler .raw file to convert"))
            {
                string directory = EditorPrefs.GetString("ProfilerRecentSaveLoadProfilePath", Application.dataPath);
                
                string filename = EditorUtility.OpenFilePanelWithFilters(EditorGUIUtility.TrTextContent("Load Window").text, directory, LoadProfilingDataFileFilters);
                if (filename.Length == 0)
                    return;
                
                _currentIndex = 0;
                
                EditorPrefs.SetString("ProfilerRecentSaveLoadProfilePath", filename);
                
                ConvertProfilerFiles(filename);
                
                GUIUtility.ExitGUI();
            }
            
        }
        
        void ConvertProfilerFiles(string value)
        {
            //Debug.Log($"Received filepath: {value}");
            string fileBase = value.Substring(0,   value.Length-4);
            string nextFile = value;
            int index = 0;
            int currentFrame = 0;
            double timeOffset = 0.0;
            while (true)
            {
                if (File.Exists(nextFile))
                {
                    try
                    {
                        ProfilerDriver.LoadProfile(nextFile, false);
                        
                        // Do conversion using profile analyzer
                        string outputFile = $"{fileBase}_frames.csv";
                        var profileAnalyzerWindow = GetWindow<ProfileAnalyzerWindow>("Profile Analyzer");
                        profileAnalyzerWindow.ConvertFromProfilerToCsv(outputFile, ref currentFrame, ref timeOffset);
            
                        Debug.Log($"File {_currentFile}_{_currentIndex}.raw converted to {outputFile}!");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Conversion process failed on {nextFile} with error {e}. skipping...");
                    }
                    
                    index++;
                    nextFile = $"{fileBase}_{index}.raw";
                }
                else
                    break;
            }

        }

    }
}
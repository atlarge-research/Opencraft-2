using UnityEditor;
using UnityEngine;

public class SetDebugPortWindow : EditorWindow
{
    [MenuItem("Window/Set Debug Port")]
    public static void ShowWindow()
    {
        //Show existing window instance. If one doesn't exist, make one.
        EditorWindow.GetWindow(typeof(SetDebugPortWindow));
    }

    void OnGUI()
    {
        GUILayout.Label("Debug Port:", EditorStyles.boldLabel);
        int value = EditorGUILayout.IntField(59820);

        if (GUILayout.Button("Set debug port"))
        {
            SetEditorBuildSettingsDebugPort(value);
        }
    }

    public void SetEditorBuildSettingsDebugPort(int port)
    {
        EditorUserBuildSettings.managedDebuggerFixedPort = port;
    }
}
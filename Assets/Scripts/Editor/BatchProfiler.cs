using System.Collections.Generic;
using System.IO;
using UnityEditor;
using Unity.EditorCoroutines.Editor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using Unity.Profiling;
using UnityEngine.Profiling;
using System;
using Unity.VisualScripting;

public class BatchProfiler : EditorWindow
{
    private List<string> apks = new List<string> { };
    private List<string> servers = new List<string>();
    private List<string> windowsPlayers = new List<string>();
    private const string controlScriptPath = "C:\\Users\\joach\\Desktop\\Opencraft-2-VR\\Assets\\Scripts\\Editor\\benchmarkScripts\\";
    private bool serverEnabled = true;
    private static string previousPathValue = "";

    [MenuItem("Window/UI Toolkit/BatchProfiler")]
    public static void ShowExample()
    {
        BatchProfiler wnd = GetWindow<BatchProfiler>();
        wnd.titleContent = new GUIContent("BatchProfiler");
    }

    private void fetchFilesFromDir(TextField pathField, ref List<string> buf, ref ListView pathList, string pattern)
    {
        buf.Clear();
        pathList.Clear();

        Debug.Log(Application.dataPath);
        Debug.Log(pathField.value);
        DirectoryInfo d = new DirectoryInfo(pathField.value);

        foreach (var file in d.GetFiles(pattern, SearchOption.AllDirectories))
        {
            buf.Add(file.FullName);
            pathList.hierarchy.Add(new Label(file.FullName));
        }
    }

    private System.Diagnostics.Process startServer(string serverpath)
    {
        Debug.Log($"starting server {serverpath}");
        System.Diagnostics.Process serverProc = new System.Diagnostics.Process();
        serverProc.StartInfo.FileName = serverpath;
        serverProc.StartInfo.Arguments = "-deploymentID 0 -deploymentJson C:\\Users\\joach\\Desktop\\Opencraft-2-VR\\Assets\\Resources\\deployment.json";
        serverProc.Start();
        return serverProc;
    }

    private System.Diagnostics.Process startHelpScript(string scriptName, string scriptPath, string arg)
    {
        Debug.Log("starting script with args: " + $"{scriptPath + scriptName} {arg}");
        System.Diagnostics.Process shellproc = new System.Diagnostics.Process();
        shellproc.StartInfo.FileName = "pwsh";
        shellproc.StartInfo.ArgumentList.Add($"{scriptPath + scriptName}");
        if (arg != "") { shellproc.StartInfo.ArgumentList.Add($"{arg}"); }
        shellproc.StartInfo.UseShellExecute = false;
        //shellproc.StartInfo.RedirectStandardOutput = true;
        //shellproc.StartInfo.RedirectStandardError = true;
        shellproc.Start();
        return shellproc;
    }
   

    private void startAndAwaitHelpScript(string scriptName, string scriptPath, string arg)
    {
        System.Diagnostics.Process shellproc = startHelpScript(scriptName, scriptPath, arg);

        shellproc.WaitForExit();
        //if (shellproc.ExitCode != 0)
        //{
        //    Debug.LogWarning("stdError: " + shellproc.StandardError.ReadToEnd());
        //}
        //Debug.Log($"script stdout: {scriptName} {shellproc.StandardOutput.ReadToEnd()}");
    }

    private string startUnityProfiler(string playerPath)
    {
        UnityEditorInternal.ProfilerDriver.ClearAllFrames();
        Profiler.enabled = true;
        string logfilePath = Application.dataPath + $"\\..\\profiler\\profiler-{Path.GetFileNameWithoutExtension(playerPath)}-{DateTime.Now.ToString().Replace("/", ".").Replace(" ", "_").Replace(":", ".")}.raw";
        FileStream fs = new FileStream(logfilePath + ".raw", FileMode.Create);
        fs.Close();
        Profiler.logFile = logfilePath;
        Profiler.enableBinaryLog = true;
        Debug.Log("application should be running!");
        return logfilePath;
    }
    
    private IEnumerator startProfiler(List<string> players)
    {
        for (int i = 0; i < players.Count; i++)
        //int i = 0;
        {
            string playerPath = players[i];

            System.Diagnostics.Process serverProc = null;
            if (serverEnabled)
            {
                serverProc = startServer(servers[i]);
            }

            UnityEngine.Debug.Log("now profiling: " + playerPath);

            startAndAwaitHelpScript($"installApk.ps1", controlScriptPath, playerPath);

            //set up profiler
            string logfilePath = startUnityProfiler(playerPath);

            //start measuring the metaverse
            System.Diagnostics.Process sysprof = startHelpScript("run.ps1", "C:\\Users\\joach\\Desktop\\measuring-the-metaverse\\", playerPath);

            //make sure the DateTime updates a
            yield return null;

            //Wait for some seconds
            DateTime start = DateTime.Now;
            int lastSecond = 0;
            while (DateTime.Now < start.AddSeconds(200))
            {
                if (DateTime.Now.Second != lastSecond)
                {
                    Debug.Log($"framed {DateTime.Now.Second} < {start.AddSeconds(200)}");
                    lastSecond = DateTime.Now.Second;   
                } 
                yield return null;
            }

            Debug.Log("application should be done!");

            //Debug.Log(sysprof.StandardOutput.ReadToEnd());
            //Debug.Log(sysprof.StandardError.ReadToEnd());
            sysprof.Kill();

            startAndAwaitHelpScript("cleanup.ps1", "C:\\Users\\joach\\Desktop\\measuring-the-metaverse\\", playerPath);

            Profiler.enabled = false;
            UnityEditorInternal.ProfilerDriver.SaveProfile(logfilePath);

            if (serverProc != null)
            {
                serverProc.CloseMainWindow();
            }

            startAndAwaitHelpScript("stopApk.ps1", controlScriptPath, "");
            Profiler.logFile = "";
            startAndAwaitHelpScript("uninstallApk.ps1", controlScriptPath, "");
        }
        Debug.Log("done");
    }

    private void setServerEnabled(Toggle serverEnabledToggle)
    {
        serverEnabled = serverEnabledToggle.value;
    }

    private void profileQuest()
    {
        this.StartCoroutine(startProfiler(apks));       
    }
    private void profileWindowsPlayers()
    {

    }

    void pathInput(string label, string defaultPath, string searchprop, ref List<string> pathList, VisualElement rt)
    {
        TextField pathField = new TextField(label);
        ListView pathListView = new ListView();
        pathField.value = defaultPath;
        fetchFilesFromDir(pathField, ref pathList, ref pathListView, searchprop);
        pathField.RegisterCallback<InputEvent>((evt) => fetchFilesFromDir(pathField, ref apks, ref pathListView, "*.apk"));
        rt.Add(pathField);
        rt.Add(pathListView);
    }

    void addButton(string label, Action clickEvent, VisualElement root)
    {
        Button button = new Button(clickEvent);
        button.Add(new Label(label));
        root.Add(button);
    }

    public void CreateGUI()
    {

        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;
        VisualElement pathFieldLabel = new Label("please enter the path of the apk folder to batch");
        root.Add(pathFieldLabel);

        string apkDefaultPath = "C:\\Users\\joach\\Desktop\\Opencraft-2-VR\\Builds\\headset_client_build";
        string serverDefaultPath = "C:\\Users\\joach\\Desktop\\Opencraft-2-VR\\Builds\\windows_builds\\server-builds";
        
        pathInput("apk path", apkDefaultPath, "*.apk", ref apks, root);
        pathInput("server path", serverDefaultPath, "*BleedingEdge.exe", ref servers, root);

        Toggle startServerCheckbox = new Toggle("start server");
        startServerCheckbox.value = serverEnabled;
        startServerCheckbox.RegisterCallback<ChangeEvent<bool>>((evt) => setServerEnabled(startServerCheckbox));
        root.Add(startServerCheckbox);


        addButton("profile apks", profileQuest, root);
        
        pathInput("windowsPlayer path", serverDefaultPath, "*BleedingEdge.exe", ref windowsPlayers, root);

        root.Add(new Label("windows experiment"));
        addButton("profile windowsPlayers", profileWindowsPlayers, root);
        
    }
}

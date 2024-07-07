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
using UnityEditor.UI;
using UnityEditorInternal;

public class BatchProfiler : EditorWindow
{
    private List<string> apks = new List<string> { };
    private List<string> servers = new List<string> { "18","3"};/*{ "10","3","4","5","6","7","8","9"};*/
    private List<string> windowsPlayers = new List<string>();
    private const string controlScriptPath = "C:\\Users\\joach\\Desktop\\Opencraft-2-VR\\Assets\\Scripts\\Editor\\benchmarkScripts\\";
    private const string serverfileName = "C:\\Users\\joach\\Desktop\\Opencraft-2-VR\\Builds\\windows_builds\\Opencraft-BleedingEdge.exe";
    private bool serverEnabled = true;
    private static string previousPathValue = "";
    System.Diagnostics.Process serverProc;
    static Unity.EditorCoroutines.Editor.EditorCoroutine routine;

    abstract class TargetPlatform
    {
        public string playerPath;
        public System.Diagnostics.Process startHelpScript(string scriptName, string scriptPath, string arg)
        {
            Debug.Log("starting script with args: " + $"{scriptPath + scriptName} \"{arg}\"");
            System.Diagnostics.Process shellproc = new System.Diagnostics.Process();
            shellproc.StartInfo.FileName = "pwsh";
            shellproc.StartInfo.ArgumentList.Add($"{scriptPath + scriptName}");
            if (arg != "") { shellproc.StartInfo.ArgumentList.Add(arg); }
            shellproc.StartInfo.UseShellExecute = false;
            shellproc.StartInfo.RedirectStandardOutput = true;
            shellproc.StartInfo.RedirectStandardError = true;
            shellproc.Start();
            return shellproc;
        }

        public void startAndAwaitHelpScript(string scriptName, string scriptPath, string arg)
        {
            System.Diagnostics.Process shellproc = startHelpScript(scriptName, scriptPath, arg);

            shellproc.WaitForExit();
            if (shellproc.ExitCode != 0)
            {
                Debug.LogWarning("stdError: " + shellproc.StandardError.ReadToEnd());
            }
            Debug.Log($"script {scriptName} stdout: {shellproc.StandardOutput.ReadToEnd()}");
        }

        public virtual void startTarget() { }
        public virtual void stopTarget() { }
        public virtual void startSysprof() { }
        public virtual void stopSysprof() { }
        public virtual void ping() { }
    }

    class TargetAndroid : TargetPlatform
    {
        System.Diagnostics.Process sysProf;
        public  override void startTarget() {
            startAndAwaitHelpScript($"installApk.ps1", controlScriptPath, playerPath);
        } 
        public override void stopTarget() {
            startAndAwaitHelpScript("stopApk.ps1", controlScriptPath, "");
            startAndAwaitHelpScript("uninstallApk.ps1", controlScriptPath, "");
        }
        public override void startSysprof() {
            sysProf = startHelpScript("run.ps1", "C:\\Users\\joach\\Desktop\\measuring-the-metaverse\\", playerPath);
        }    
        public override void stopSysprof() {
            if (sysProf != null)
            {
                if (!sysProf.HasExited)
                {
                    sysProf.CloseMainWindow();
                    sysProf.WaitForExit();
                }

                Debug.Log("sysProf StdOut = " + sysProf.StandardOutput.ReadToEnd());
                Debug.Log("sysProf StdError = " + sysProf.StandardError.ReadToEnd());
                startAndAwaitHelpScript("cleanup.ps1", "C:\\Users\\joach\\Desktop\\measuring-the-metaverse\\", playerPath);
            }
            else
            {
                Debug.LogWarning("attempted close of sysprof, but sysprof is null");
            }
        }
        ~TargetAndroid()
        {
            if (!sysProf.HasExited)
            {
                sysProf.Kill();
                stopTarget();
            }
        }
    }

    class TargetLinux : TargetPlatform
    {
        System.Diagnostics.Process sshTargetTask;
        System.Diagnostics.Process sysProf;
        string helpScriptPath = "C:\\Users\\joach\\Desktop\\Opencraft-2-VR\\Assets\\Scripts\\Editor\\benchmarkScriptsLin\\";
        string sysProfPath = "C:\\Users\\joach\\Desktop\\measuring-the-metaverse\\";
        public override void startTarget() {
            sshTargetTask = startHelpScript("start.ps1", helpScriptPath, playerPath);
        }
        public override void stopTarget() {
            sshTargetTask.CloseMainWindow();
            
            try
            {
                sshTargetTask.Kill();
            }
            catch (Exception)
            {
            }
            
            startAndAwaitHelpScript("stop.ps1", helpScriptPath, playerPath);
        }
        public override void startSysprof() {
            sysProf = startHelpScript("runLinux.ps1", sysProfPath, playerPath);
        }
        public override void stopSysprof() { 
            sysProf.CloseMainWindow();
            sysProf.WaitForExit();
            startAndAwaitHelpScript("cleanup.ps1", sysProfPath, playerPath);
        }

        public override void ping()
        {
            Debug.Log(sshTargetTask.StandardOutput.ReadToEnd());
        }

        ~TargetLinux()
        {
            sysProf.Kill();
            sshTargetTask.Kill();
            Debug.Log(sshTargetTask.StandardOutput.ReadToEnd());
            Debug.Log(sshTargetTask.StandardError.ReadToEnd());
        }
    }

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

        //Debug.Log(Application.dataPath);
        //Debug.Log(pathField.value);
        DirectoryInfo d = new DirectoryInfo(pathField.value);

        foreach (var file in d.GetFiles(pattern, SearchOption.AllDirectories))
        {
            buf.Add(file.FullName);
            pathList.hierarchy.Add(new Label(file.FullName));
        }
    }

    private System.Diagnostics.Process startServer(string serverRenderDist)
    {
        Debug.Log($"starting server {serverRenderDist}");
        System.Diagnostics.Process serverProc = new System.Diagnostics.Process();
        serverProc.StartInfo.FileName = serverfileName;
        serverProc.StartInfo.Arguments = $"-deploymentID 0 -deploymentJson " +
            $"C:\\Users\\joach\\Desktop\\Opencraft-2-VR\\Assets\\Resources\\deploymentMul.json" +
            $" -renderDist {serverRenderDist} -playerFly";
        serverProc.Start();
        return serverProc;
    }

    private string startUnityProfiler(string playerPath)
    {

        //ProfilerDriver.DirectIPConnect("192.168.23.94");
        Profiler.enabled = true;

        string playerFileNameNoExt = playerPath;
        if(playerPath.Contains('.'))
        {
            string processedPlayerPath = playerPath.Replace("/", "\\");
            string playerFileName = playerPath.Substring(processedPlayerPath.LastIndexOf("\\") + 1);
            playerFileNameNoExt = playerFileName.Substring(0, playerFileName.LastIndexOf("."));
        }
        
        string logfilePath = Application.dataPath + $"\\..\\profiler\\profiler-{playerFileNameNoExt}-{DateTime.Now.ToString().Replace("/", ".").Replace(" ", "_").Replace(":", ".")}";

        Debug.Log($"logfilepath = {logfilePath}");
        //FileStream fs = new FileStream(logfilePath + ".raw", FileMode.Create);
        //fs.Close();
        Profiler.logFile = logfilePath;
        Profiler.enableBinaryLog = true;
        Debug.Log("application should be running!");
        
        return logfilePath;
    }
    
    private IEnumerator startProfiler(List<string> players, TargetPlatform target)
    {
        for (int i = 0; i < players.Count; i++)
        //int i = 0; 
        {
            string playerPath = players[i];
            target.playerPath = playerPath;

            serverProc = null;
            if (serverEnabled)
            {
                servers.ForEach(Debug.Log);
                Debug.Log(servers[i]);
                serverProc = startServer(servers[i]);
            }

            UnityEngine.Debug.Log($"step{i + 1}/{players.Count} now profiling: {playerPath}");

            target.startTarget();

            //set up profiler
            yield return null;
            UnityEditorInternal.ProfilerDriver.ClearAllFrames();
            yield return null;
            
            DateTime start = DateTime.Now;
            //int preTimeout = 0;
            //Debug.Log($"waiting for {start.AddSeconds(preTimeout)}");
            //while (DateTime.Now < start.AddSeconds(preTimeout))
            //{
            //    yield return null;
            //}
            
            string logfilePath = startUnityProfiler(playerPath);

            //start measuring the metaverse
            target.startSysprof();

            //make sure the DateTime updates after possible long installation times 
            yield return null;

            //Wait for some seconds
            start = DateTime.Now;
            int lastSecond = 0;
            int timeout = 150;
            while (DateTime.Now < start.AddSeconds(timeout))
            {
                if (DateTime.Now.Second != lastSecond)
                {
                    Debug.Log($"framed {DateTime.Now.Second} < {start.AddSeconds(timeout)}");
                    lastSecond = DateTime.Now.Second;
                    //target.ping();
                } 
                yield return null;
            }

            Debug.Log("application should be done!");

            //Debug.Log(sysprof.StandardOutput.ReadToEnd());
            //Debug.Log(sysprof.StandardError.ReadToEnd());
            target.stopSysprof();

            Profiler.enabled = false;
            UnityEditorInternal.ProfilerDriver.SaveProfile(logfilePath + ".prof");

            if (serverProc != null)
            {
                serverProc.CloseMainWindow();
            }

            target.stopTarget();
            Profiler.logFile = "";
            yield return null; //make sure the profiler has proper time to update itself
        }
        Debug.Log("done");
        routine = null; //mark finished
    }

    private void setServerEnabled(Toggle serverEnabledToggle)
    {
        serverEnabled = serverEnabledToggle.value;
    }

    private void startAndSaveCoroutine(IEnumerator newRoutine)
    {
        if (routine != null)
        {
            Debug.LogWarning("only one profiler may be running at a time");
            return;
        }
        routine = this.StartCoroutine(newRoutine);
    }

    private void profileQuest()
    {
        startAndSaveCoroutine(startProfiler(apks, new TargetAndroid()));       
    }
    private void profileLinuxPlayers()
    {

        //System.Diagnostics.Process lsProc = new System.Diagnostics.Process();
        //lsProc.StartInfo.Arguments = "-t joachim@192.168.23.94 \"find ~/Opencraft2 -type f -name '*.x86_64'\"";
        //lsProc.StartInfo.FileName = "C:\\WINDOWS\\System32\\OpenSSH\\ssh.exe";
        //lsProc.StartInfo.UseShellExecute = false;
        //lsProc.StartInfo.RedirectStandardOutput = true;
        //lsProc.StartInfo.RedirectStandardError = true;


        //lsProc.Start();
        //lsProc.WaitForExit();
        //List<string> paths = new List<string>(lsProc.StandardOutput.ReadToEnd().Split("\r\n"));
        //if (lsProc.ExitCode > 0) {
        //    Debug.Log($"ssh ls exited with {lsProc.ExitCode}, where stderr: {lsProc.StandardError.ReadToEnd()}");
        //}
        List<string> paths = new List<string> { "18" };
        //for (int i = 6; i < 43; i += 6)
        //{
        //    paths.Add(i.ToString());
        //}
        startAndSaveCoroutine(startProfiler(paths, new TargetLinux()));
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
    void stopExperiment()
    {
        this.StopCoroutine(routine);
        routine = null;

        serverProc.Kill();
    }

    void startServerAction()
    {
        startServer("18");
    }

    public void CreateGUI()
    {

        // Each editor window contains a root VisualElement object 
        VisualElement root = rootVisualElement;
        VisualElement pathFieldLabel = new Label("please enter the path of the apk folder to batch");
        root.Add(pathFieldLabel);

        string apkDefaultPath = "C:\\Users\\joach\\Desktop\\Opencraft-2-VR\\Builds\\HMD-sing-tracing";
        string serverDefaultPath = "C:\\Users\\joach\\Desktop\\Opencraft-2-VR\\Builds\\windows_builds";
        string defaultRemote = "joachim@192.168.23.94";

        pathInput("apk path", apkDefaultPath, "*.apk", ref apks, root);
        //pathInput("server path", serverDefaultPath, "*BleedingEdge.exe", ref servers, root);

        Toggle startServerCheckbox = new Toggle("start server");
        startServerCheckbox.value = serverEnabled;
        startServerCheckbox.RegisterCallback<ChangeEvent<bool>>((evt) => setServerEnabled(startServerCheckbox));
        root.Add(startServerCheckbox);


        addButton("profile apks", profileQuest, root);//as

        root.Add(new Label("linux experiment"));
        addButton("profile linuxPlayers", profileLinuxPlayers, root);
        addButton("force stop experiment", stopExperiment, root);
        addButton("startServer", startServerAction, root);
    }
}

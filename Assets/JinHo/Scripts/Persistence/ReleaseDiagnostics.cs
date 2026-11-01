using System;
using System.IO;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;

/// <summary>Local, bounded diagnostics only; no upload and no second gameplay manager.</summary>
public static class ReleaseDiagnostics
{
    static string logPath;
    static double lastFrame,windowStart,sceneStarted;
    static int frames,slowFrames;
    static double totalMs,maxMs;
    public static string Folder
    {
        get
        {
#if UNITY_EDITOR
            return Path.Combine(Application.temporaryCachePath,"BatterMapDiagnostics");
#else
            return Path.Combine(Application.persistentDataPath,"Diagnostics");
#endif
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Application.logMessageReceived-=Error;SceneManager.activeSceneChanged-=SceneChanged;
        logPath=null;lastFrame=windowStart=sceneStarted=0;frames=slowFrames=0;totalMs=maxMs=0;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        try
        {
            string folder=Folder; // Recording and the folder button share one environment-specific path.
            Directory.CreateDirectory(folder);
            logPath=Path.Combine(folder,"session-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".log");
            Write("START version="+Application.version+" build="+Application.buildGUID+" saveSchema="+ReleaseProgressFiles.CurrentVersion+" unity="+Application.unityVersion+" platform="+Application.platform+
                " cpu="+SystemInfo.processorType+" ramMB="+SystemInfo.systemMemorySize+" gpu="+SystemInfo.graphicsDeviceName+" vramMB="+SystemInfo.graphicsMemorySize+" display="+Screen.width+"x"+Screen.height+" development="+UnityEngine.Debug.isDebugBuild);
            Application.logMessageReceived+=Error;SceneManager.activeSceneChanged+=SceneChanged;
            var files=new DirectoryInfo(folder).GetFiles("session-*.log");Array.Sort(files,(a,b)=>b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            for(int i=10;i<files.Length;i++)try{files[i].Delete();}catch(IOException){}
        }
        catch(Exception){logPath=null;} // Diagnostics failure must never block gameplay/saving.
    }
    static void Write(string message)
    {
        if(logPath==null)return;
        try{if(File.Exists(logPath)&&new FileInfo(logPath).Length>4*1024*1024)return;File.AppendAllText(logPath,DateTime.UtcNow.ToString("O")+" "+message+Environment.NewLine);}catch(Exception){}
    }
    static void Error(string message,string stack,LogType type)
    {
        if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
        Write("ERROR scene="+SceneManager.GetActiveScene().name+" day="+SmithingLoop.Instance?.SmithData.day+" story="+SmithingLoop.Instance?.Campaign.story?.step+" "+message+" "+stack);
    }
    static void SceneChanged(Scene previous,Scene next)
    {Write("SCENE "+previous.name+" -> "+next.name+" elapsedMs="+(sceneStarted>0?(Time.realtimeSinceStartupAsDouble-sceneStarted)*1000:0).ToString("0.0")+" allocatedMB="+(Profiler.GetTotalAllocatedMemoryLong()/1048576d).ToString("0.0"));sceneStarted=0;lastFrame=windowStart=0;frames=slowFrames=0;totalMs=maxMs=0;}
    public static void BeginScene(string path){sceneStarted=Time.realtimeSinceStartupAsDouble;Write("SCENE_REQUEST "+Path.GetFileNameWithoutExtension(path));}
    public static void Tick()
    {
        double now=Time.realtimeSinceStartupAsDouble;if(lastFrame<=0){lastFrame=windowStart=now;return;}
        double ms=(now-lastFrame)*1000;lastFrame=now;totalMs+=ms;maxMs=Math.Max(maxMs,ms);frames++;if(ms>16.667)slowFrames++;
        if(now-windowStart<30)return;
        Write("PERF scene="+SceneManager.GetActiveScene().name+" frames="+frames+" meanMs="+(totalMs/Math.Max(1,frames)).ToString("0.00")+" maxMs="+maxMs.ToString("0.00")+" over16_67ms="+slowFrames+" paused="+(Time.timeScale==0)+" allocatedMB="+(Profiler.GetTotalAllocatedMemoryLong()/1048576d).ToString("0.0"));
        windowStart=now;frames=slowFrames=0;totalMs=maxMs=0;
    }
    public sealed class Measurement:IDisposable
    {
        readonly string name;readonly Stopwatch clock=Stopwatch.StartNew();
        internal Measurement(string name){this.name=name;}
        public void Dispose(){clock.Stop();Write(name+" ms="+clock.Elapsed.TotalMilliseconds.ToString("0.00")+" scene="+SceneManager.GetActiveScene().name);}
    }
    public static Measurement Measure(string name)=>new(name);
    public static void OpenFolder(){try{Directory.CreateDirectory(Folder);Application.OpenURL(new Uri(Folder+Path.DirectorySeparatorChar).AbsoluteUri);}catch(Exception){}}
}

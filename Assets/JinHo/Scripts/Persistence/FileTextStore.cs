// Durable UTF-8 temp write followed by atomic replace and previous-file backup.
// 주요 함수: Write, File, FileTextStore
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Persistence/FileTextStore.cs.md

using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>Separates Editor Play Mode game progress from installed-player progress.</summary>
public static class GameSavePaths
{
    static readonly string uiTestSession = Guid.NewGuid().ToString("N");
    public static bool IsUITest => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "UITestScene";
    public static string Root
    {
        get
        {
            if (IsUITest) return Path.Combine(Application.temporaryCachePath, "UITestSession", uiTestSession);
#if UNITY_EDITOR
            string isolated = EditorGodModeSession.ActiveRoot;
            if (!string.IsNullOrEmpty(isolated)) return isolated;
            return Path.Combine(Application.persistentDataPath, "EditorPlayMode");
#elif UNITY_STANDALONE_WIN
            // Stable across updates; legacy build-GUID folders remain import sources.
            return Path.Combine(Application.persistentDataPath, "Release");
#else
            return Application.persistentDataPath;
#endif
        }
    }

    public static string File(string fileName) => Path.Combine(Root, fileName);
}

/// <summary>Stores text in files for the ITextStore contract.</summary>
public sealed class FileTextStore : ITextStore
{
    private readonly string path;
    public FileTextStore(string path) => this.path = path;
    public bool Exists => File.Exists(path);
    public string Read() => File.ReadAllText(path, Encoding.UTF8);
    public void Write(string text)
    {
        using var measurement=ReleaseDiagnostics.Measure("SAVE_FILE");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporaryPath = path + ".tmp";
        try
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            using (var file = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
            if (File.Exists(path)) File.Replace(temporaryPath, path, path + ".bak");
            else File.Move(temporaryPath, path);
        }
        catch
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }
    public void Flush() { }
}

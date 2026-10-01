// [코드 지도] FileTextStore: ITextStore를 파일 시스템으로 구현한다. UTF-8 텍스트를 임시 파일에 쓴 뒤 대상 파일로 복사하고 임시 파일을 지운다. File.Copy를 사용하므로 원자적 교체가 보장된다고 설명해서는 안 된다.
// 주요 함수: Write, File, FileTextStore
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Persistence/FileTextStore.cs.md

using System.IO;
using System.Text;
using UnityEngine;

/// <summary>Separates Editor Play Mode game progress from installed-player progress.</summary>
public static class GameSavePaths
{
    public static string Root
    {
        get
        {
#if UNITY_EDITOR
            string isolated = EditorGodModeSession.ActiveRoot;
            if (!string.IsNullOrEmpty(isolated)) return isolated;
            return Path.Combine(Application.persistentDataPath, "EditorPlayMode");
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
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, text, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(temporaryPath, path, path + ".bak");
        else File.Move(temporaryPath, path);
    }
    public void Flush() { }
}

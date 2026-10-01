using System;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Clears Editor-only game progress before any player build.</summary>
public sealed class EditorPlayModeSaveBuildCleanup : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        string persistentRoot = Path.GetFullPath(Application.persistentDataPath);
        string editorSaveRoot = Path.GetFullPath(Path.Combine(persistentRoot, "EditorPlayMode"));
        if (!string.Equals(Path.GetDirectoryName(editorSaveRoot), persistentRoot,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetFileName(editorSaveRoot), "EditorPlayMode",
                StringComparison.Ordinal))
            throw new BuildFailedException("Editor save cleanup refused an unexpected path: " + editorSaveRoot);

        if (!Directory.Exists(editorSaveRoot)) return;

        try
        {
            EnsureNoReparsePoints(editorSaveRoot);
            Directory.Delete(editorSaveRoot, true);
        }
        catch (IOException exception)
        {
            throw new BuildFailedException("Could not clear Editor Play Mode saves before building: " + exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new BuildFailedException("Could not clear Editor Play Mode saves before building: " + exception.Message);
        }

        Debug.Log("Cleared Editor Play Mode game saves before player build: " + editorSaveRoot);
    }

    private static void EnsureNoReparsePoints(string directory)
    {
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new BuildFailedException("Editor save cleanup refused a linked directory: " + directory);

        foreach (string entry in Directory.GetFileSystemEntries(directory))
        {
            FileAttributes attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new BuildFailedException("Editor save cleanup refused a linked entry: " + entry);
            if ((attributes & FileAttributes.Directory) != 0)
                EnsureNoReparsePoints(entry);
        }
    }
}

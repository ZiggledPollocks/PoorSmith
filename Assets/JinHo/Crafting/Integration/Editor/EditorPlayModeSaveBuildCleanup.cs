using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>Preserves existing saves when creating a new player build.</summary>
public sealed class EditorPlayModeSaveBuildCleanup : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        // GameSavePaths isolates Editor progress and gives each Windows build its own
        // save directory. A build must never erase an existing user's progress.
    }
}

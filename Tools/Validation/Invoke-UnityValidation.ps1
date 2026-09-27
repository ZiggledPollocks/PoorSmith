[CmdletBinding()]
param(
    [string]$ProjectPath = (Join-Path $PSScriptRoot '../..'),
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Unity.exe',
    [string]$RunRoot = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Codex/UV'),
    [ValidateRange(1, 7200)][int]$TimeoutSeconds = 1200,
    [string]$PythonPath = 'python',
    [switch]$NegativeControl
)
$ErrorActionPreference = 'Stop'
$runnerArgs = @('-X', 'utf8', (Join-Path $PSScriptRoot 'unity_validation.py'),
    '--project', $ProjectPath, '--unity', $UnityPath, '--run-root', $RunRoot,
    '--timeout', $TimeoutSeconds.ToString())
if ($NegativeControl) { $runnerArgs += '--negative-control' }
& $PythonPath @runnerArgs
exit $LASTEXITCODE

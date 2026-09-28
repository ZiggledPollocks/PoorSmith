[CmdletBinding()]
param(
    [string]$ProjectPath = (Join-Path $PSScriptRoot '../..'),
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Unity.exe',
    [string]$RunRoot = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Codex/UV'),
    [ValidateRange(1, 7200)][int]$TimeoutSeconds = 1200,
    [string]$PythonPath = 'python'
)
$ErrorActionPreference = 'Stop'
& $PythonPath -X utf8 (Join-Path $PSScriptRoot 'runtime_validation.py') --project $ProjectPath --unity $UnityPath --run-root $RunRoot --timeout $TimeoutSeconds
exit $LASTEXITCODE

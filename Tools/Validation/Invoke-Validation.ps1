[CmdletBinding()]
param(
    [ValidateSet('plan','run')][string]$Action = 'plan',
    [string]$ProjectPath = (Join-Path $PSScriptRoot '../..'),
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Unity.exe',
    [string]$RunRoot = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Codex/UV'),
    [string]$Scope = '', [string]$Require = '', [string[]]$Changed = @(),
    [ValidateSet('reuse','fresh')][string]$Mode = 'reuse',
    [string]$Workspace = '', [string]$ForceRecheck = '',
    [string]$RetryEnvironmentReason = '', [string]$CauseId = '',
    [string]$Approach = 'existing-fixture',
    [ValidateRange(0,30)][int]$LockWaitSeconds = 0,
    [ValidateRange(1,7200)][int]$TimeoutSeconds = 1200,
    [string]$PythonPath = 'python'
)
$ErrorActionPreference = 'Stop'
$runnerArgs = @('-X','utf8',(Join-Path $PSScriptRoot 'validation_workflow.py'),$Action,
    '--project',$ProjectPath,'--unity',$UnityPath,'--run-root',$RunRoot,
    '--mode',$Mode,'--timeout',$TimeoutSeconds.ToString(),'--lock-wait',$LockWaitSeconds.ToString(),
    '--approach',$Approach)
foreach ($pair in @(@('--scope',$Scope),@('--require',$Require),@('--workspace',$Workspace),
    @('--force-recheck',$ForceRecheck),@('--retry-environment-reason',$RetryEnvironmentReason),@('--cause-id',$CauseId))) {
    if ($pair[1]) { $runnerArgs += $pair }
}
foreach ($path in $Changed) { $runnerArgs += @('--changed',$path) }
& $PythonPath @runnerArgs
exit $LASTEXITCODE

# Unity M1 validation runner

Requirements: Windows, PowerShell, Python 3.10+ (standard library only), Git, and the exact Hub-installed Unity version in ProjectSettings/ProjectVersion.txt. Do not pass a source project with unreviewed editor startup plugins: compilation/import can execute Editor initialization code. Reviewed batterMap startup hooks do not run the UI generator in batch mode; this runner never invokes that generator or Play Mode.

From the repository root:

```powershell
& ./Tools/Validation/Invoke-UnityValidation.ps1
```

Optional explicit paths:

```powershell
& ./Tools/Validation/Invoke-UnityValidation.ps1 `
  -ProjectPath 'C:/programming/Projects/unity/batterMap' `
  -UnityPath 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Unity.exe' `
  -RunRoot 'C:/Users/Master/Documents/Codex/UV' `
  -TimeoutSeconds 1200
```

The runner snapshots saved Assets (including .meta), Packages and ProjectSettings, including dirty/untracked files. Unsaved editor memory, Library, UserSettings, .git and generated root IDE files are not copied. Reparse points and local file-package dependencies are rejected until their copy policy is explicitly implemented. Registry dependencies may need network/cache access. No package upgrades or API updates are requested. No reusable cache or cleanup is implemented. The Windows project-path prefix must be at most 64 characters; use the short default Documents/Codex/UV. This reduces observed importer path-limit failures but is not a guarantee for all future packages.

A compile-completion probe is stored as CompileProbe.cs.txt under Tools. It is injected only into the isolated copy at Assets/__BatterMapValidation/Editor. This avoids importing a new C# script in the user's live project. Each run uses a fresh ID/path; the original is never launched.

The compile target is explicitly Windows64 editor compilation, not a player build or all-platform build. It runs without graphics and does not check shaders, layout, gameplay, UI transitions or saved-game restoration.

Success requires exit code 0, a matching run/project/version/target marker and the completion log line; compiler errors, asset-import failures and snapshot mutations prevent success. Recovered startup licensing warnings alone do not override positive completion. Pre/post source hashes establish whether the disk inputs remained unchanged. Concurrent changes are preserved and invalidate attribution rather than being overwritten.

Outputs: run-root/run-id/evidence/{snapshot.json,result.json,completion.json,summary.md,logs/}. completion.json exists only if the entry point executed. The result records the exact command, exit code, elapsed time, source drift and per-scope limitations. Internal Unity import may add .meta files; modifications to original snapshot files are reported separately and cannot silently pass.

Exit codes: 0 passed; 1 failed; 2 blocked/preflight failure. Preflight failure before safe directory allocation prints JSON without creating a report folder. An outer timeout kills only the owned Unity process; it never kills the user's editor by process name. It does not guarantee every Unity helper process exits immediately after an abnormal termination.

## Validator self-checks

Fast guard/outcome tests:

```powershell
python -X utf8 -m unittest discover -s Tools/Validation -p test_unity_validation.py -v
```

Real Unity negative control (makes another full disposable snapshot):

```powershell
& ./Tools/Validation/Invoke-UnityValidation.ps1 -NegativeControl
```

Negative control injects an intentional #error only in the disposable copy. The compile result must be failed/compiler_error, contain BATTERMAP_EXPECTED_NEGATIVE_CONTROL, have no completion marker, and preserve the original. The negative-control command returns 0 when this expected failure is correctly detected. Read negative_control_detected separately from the game's compile status.

## Evidence limits

No license bypass, credential copying, scene save/reload in the original, user save mutation, Git commit or push occurs. This is not a generic sandbox for arbitrary untrusted Editor code. Custom initialization/plugins must be reassessed when changed. Read Docs/Validation.md for actual trial results and known limitations.

Command/API reference: [Unity 6.3 command-line arguments](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html).

## M2 UI and M3 inventory runtime validation

From the repository root:

```powershell
& ./Tools/Validation/Invoke-RuntimeValidation.ps1
python -X utf8 -m unittest discover -s Tools/Validation -p 'test_*.py' -v
```

Windows-only runtime runner. Uses the same Python/Unity/short-run-root options as M1 except there is no NegativeControl switch. Runs saved SampleScene and GameUI assets without generators. Graphics are enabled for canvas rendering; no full Game View or physical mouse coverage is implied. Inventory toggle requests are injected at PlayerInputHandler; interactive movement rebinding uses queued Input System events.

Before launch, only the copied ProjectSettings companyName/productName are changed to BatterMapValidation/Run_<unique ID>. The probe verifies identity before Play Mode. Original JSON saves and Windows Editor/standalone PlayerPrefs registry subtrees are hashed before/after. Read-only access failure blocks the run. Unique test data is retained in LocalLow/BatterMapValidation/Run_<ID> and the matching UnityEditor registry namespace; no user save is cleared or restored.

Evidence includes per-assertion completion.json, PNG canvas renders, source manifest, approved copied-settings override, logs, original persistence before/after hashes and isolated-persistence-after.json. Pass requires process success, current completion, successful checks, no recorded runtime errors, unchanged original data/source and no unexpected copy mutation. Runtime exit code is 0 only on pass; 1 otherwise; inspect status/reason to distinguish failed and blocked.

Legacy Assets/JinHo/Editor/UIIntegrationSmokeTest.cs is not invoked. Its generation step and old startup assertion are superseded by RuntimeProbe.cs.txt for this workflow. The old source is preserved for history; do not use it as the current verification entry point.

M3 currently covers inventory model boundaries and quantity preservation only. Extend checks to the affected gameplay when future changes require it. This does not approve product bug fixes or certify combat, portals, movement, physical pickup/drop, corrupted saves or player builds.

## C003 movement checks

The runtime runner also injects MovementCases.cs.txt into the disposable project. It runs deterministic mode calculations and synchronous Play Mode component integration checks, and requires `Movement refactor checks completed` in the completion marker. This does not verify physical trigger traversal, real keyboard gameplay, or visual feel. The saved UI/inventory scenarios continue to run. See Docs/Evidence/C003-movement-results.json.

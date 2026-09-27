# Unity validation

Updated 2026-09-23. M1 remains verified. M2 UI and first M3 inventory checks are approved, implemented and executed; latest overall runtime result: passed. See the M2/M3 section below.

## Verified usage

From the repository root in PowerShell:

```powershell
& ./Tools/Validation/Invoke-UnityValidation.ps1
```

Requirements: Windows, Git, Python 3.10+ available as python (or supply -PythonPath), installed Unity matching ProjectSettings/ProjectVersion.txt, and available Unity license/package access. Actual tested Unity: 6000.3.11f1. Default run root: current user's Documents/Codex/UV. Use a short path; the runner rejects project prefixes longer than 64 characters to reduce Windows importer path-limit failures. This bound is a guard, not proof that all future package filenames will fit.

The runner snapshots saved Assets, Packages and ProjectSettings including .meta and uncommitted/untracked files. It excludes Library, Temp, UserSettings, .git and generated root IDE files. Unsaved editor memory is not included. External local packages and reparse points are rejected pending explicit support.

The Python runner writes a unique snapshot and SHA-256 manifest, injects a completion probe only into the copy, launches batch/no-graphics Unity for Windows64 editor compilation, and records exit status plus positive completion evidence. No UI generator, scene Play Mode or player build is invoked. CompileProbe.cs.txt stays outside the live Assets folder. See [tool usage](../Tools/Validation/README.md).

## M1 historical results

| Check | Result |
|---|---|
| Guard/outcome tests | 12 passed; overlap, existing target, missing executable, path length, unsupported dependency, source drift, timeout, stale/missing marker, compiler/import failure and recovered licensing warning |
| Real normal run | passed; exit 0, matching completion marker, no C# compiler errors or asset-import failures |
| Real negative control | deliberate #error detected; compile failed as expected, no completion marker, negative_control_detected=true |
| Original disk inputs | all 4,956 selected input files preserved in both final runs |
| UI / Play Mode / player build / visual behavior | not_run |

Normal run: `C:/Users/Master/Documents/Codex/UV/262e31168003` (297.54 seconds including copying/hashing).
Negative run: `C:/Users/Master/Documents/Codex/UV/5ae46b49ec59` (200.07 seconds).
Structured evidence: [M1 results](Evidence/M1-validation-results.json). Full logs/manifests remain in each run's evidence directory.

Initial trial: a longer output path caused package asset-import DirectoryNotFoundException errors even though the C# completion marker was reached. The initial classifier also mistook a recovered startup licensing warning for a final blocker. We shortened the default path, added path-length and import-error guards, corrected licensing classification and added regression tests, then ran the final trials. Original project packages were not upgraded or edited. Initial logs remain preserved.

## Result semantics and limitations

Pass requires exit 0, the current run/project/version/Windows64-target marker, a completion log line, no detected compiler/import failures, no changes to original snapshot files and no source drift. Compiler failure is failed; prerequisite failures/timeouts are blocked; unselected stages are not_run. Concurrent user changes invalidate attribution without being overwritten.

The report includes command, version, Git context, file hashes, elapsed time, diagnostics and evidence locations. A fresh unique output directory is used every time. No automatic deletion or cache reuse is implemented. First import is relatively expensive.

M1 does not execute scene gameplay save hooks. Reviewed source startup hooks skip the UI generator in batch mode. This is not a general security sandbox: new Editor initialization/plugins need side-effect review. Disk source preservation is hash-verified; normal gameplay save behavior and save isolation during Play Mode remain M2 concerns.

The compile target is Windows64 editor compilation, not all platform symbols, a player build, a visual check or gameplay success. Some no-graphics shader warnings may appear and are outside visual validation.

## Validator self-check commands

```powershell
python -B -X utf8 -m unittest discover -s Tools/Validation -p test_unity_validation.py -v
& ./Tools/Validation/Invoke-UnityValidation.ps1 -NegativeControl
```

The negative-control command returns 0 when the expected compiler failure is correctly detected and source is preserved. Its recorded Unity compile status remains failed. Never present this as a passing game compile.

## Legacy UI test — superseded

Assets/JinHo/Editor/UIIntegrationSmokeTest.cs is batch-only, invokes SettingsMenuUIBuilder, writes assets/settings, opens SampleScene and enters Play Mode. Its initial immediate-gameplay assertion disagrees with the enabled start menu. Separate saved-asset checks from generator checks, reconcile expectations and isolate all persistence before using it. Do not run it on the live editor project.

ResourceSpawnZoneSceneSetup also has mutating setup methods. No such setup methods ran during M1.

## Additional change-scoped candidates

Select only relevant scenarios: UI modal transitions and rebinding; inventory weight boundaries and pickup; offering return/zero-assimilation semantics; movement/roll/slopes/fall; monster collision and boss re-entry; portal fade/bounds/themes; isolated save corruption/version handling. Record target scene, overrides, steps, expected/observed outcomes and visual evidence. These are future candidates, not current test passes or authorized gameplay fixes.

Reference: [Unity 6.3 command-line arguments](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html).

## M2/M3 actual execution — 2026-09-23

Both milestones were approved. Runtime harness implementation is complete. Latest run: **passed**, reason `runtime_completion_confirmed`; 236/236 recorded assertions passed (including 10/10 M3 inventory checks). Guard tests: 17 passed. Runtime duration including snapshot/import: 351.49 seconds. Original source preserved: True; user persistence preserved: True.

Run: `C:/Users/Master/Documents/Codex/UV/bea366455a09`. Structured evidence: [M2/M3 results](Evidence/M2-M3-validation-results.json). Detailed scope: [runtime specification](Cycles/C002-runtime-validation.md).

```powershell
& ./Tools/Validation/Invoke-RuntimeValidation.ps1
```

The first run (285fc64ca59b) passed the UI flow and all 10 inventory model checks, but correctly failed the overall no-error requirement due to ArgumentOutOfRangeException in UnityEditor.Search.SearchDatabase during editor initialization. No game code appears in that exception stack. The harness now allows an eight-second quiet import/compilation interval before entering Play Mode. The retry result above is observed evidence; it does not prove the precise editor-race cause or permanent elimination. Both trial logs are retained. All project inputs and original user persistence remained unchanged in the first run too.

Company/product names are changed only in the copied ProjectSettings before launch. Persistent JSON and Windows PlayerPrefs use a unique test namespace. Copied Assets are not rebuilt; unexpected changes to existing snapshot inputs fail the run. Every run retains its own test data; no automatic cleanup occurs.

The current runtime probe supersedes the legacy UIIntegrationSmokeTest entry point. It is a Tools text template, injected into the copy only. Five 1920x1080 canvas renders cover menu, display settings, controls, sound and empty inventory. These are isolated canvas views rather than complete Game View captures. Input request injection does not verify physical keyboard-to-inventory action wiring; direct controller calls do not verify mouse raycasts or every button listener. M3 covers inventory weight/count/events, not every gameplay system. Player builds, unsaved editor state, combat, movement physics, portals, offering, save corruption and process-restart persistence remain unverified.

## C003 movement refactor — 2026-09-26

Isolated Unity editor compilation and Play Mode assertions passed: 300/300 checks, including 64 movement assertions. Runner guards: 17 passed. Overall run BLOCKED by original source and user persistence drift; preservation not established. Auto-review rejected retry and no retry executed. A separate original Unity process was present; drift attribution is unproven. MovementCases.cs.txt is injected beside RuntimeProbe.cs.txt; the completion classifier requires its completion assertion. Cases cover ground/air/reversal/wind/jump/gravity numbers, Flame release/resistance, UpDraft target/float behavior, generic mode dispatch, priority/idempotence/invalid handles, destroyed sources, multi-collider exit, unchanged Y exit velocity, overlapping regions, roll/knockback, and disable/re-enable cleanup.

Evidence: [C003 result](Evidence/C003-movement-results.json). Full run: `C:/Users/Master/Documents/Codex/UV/8aca78039e5d`. Integration checks invoke real component callbacks synchronously in Play Mode on disposable objects; they do not validate physical keyboard or actual physics-trigger traversal. Visual behavior and player builds remain not_run. No live scene reload or save occurred.

## C004 offline script architecture check — 2026-09-26

After 84 first-party script dispositions, the installed source matched the staged hashes and all 59 moved script `.meta` files matched their originals byte-for-byte. Offline Roslyn compilation against the installed Unity references passed for Assembly-CSharp and Assembly-CSharp-Editor (0 errors). This did not start the Unity editor or validate asset import, scene references, gameplay, visuals, save restart or builds. A separate standalone behavior-comparison executable failed with an unhandled .NET exception and must not be cited as a passing test. See [C004 evidence](Evidence/C004-script-refactor.json).

## C005 callback input — 2026-09-26

Installed Unity-reference Roslyn compilation passed for Assembly-CSharp and Assembly-CSharp-Editor (0 errors). The staged inputactions JSON preserved all existing Player actions and bindings and added only ToolScroll plus its mouse-wheel binding, with unique IDs. Source, interaction component and inputactions GUID metadata stayed unchanged. PlayerInteraction button polling was removed; the complete staged runtime and Editor code compiled with zero errors. This does not establish Unity asset import, mouse-wheel direction, keyboard/gamepad event phases or UI blocking at runtime. See [C005 scope](Cycles/C005-callback-input.md).

## C006 per-action callback refactor — 2026-09-26

Runtime and Editor offline Roslyn assembly compilation passed with zero errors. Static checks found all 11 action-specific callbacks with matching subscription/removal phases, unchanged public consume methods and serialized toolController field, and no Update/global dispatcher. The original script GUID metadata was unchanged. This is not Unity Play Mode or actual input-device verification. See [C006 specification](Cycles/C006-per-action-callbacks.md).


## C008 campaign — 2026-09-27

Use Tools/CampaignValidation/Invoke-RuntimeValidation.ps1 for the new saved NotionCampaign scene. The existing Tools/Validation entry remains the SampleScene suite. C008 checks economy, input/movement, day/night, real shop buttons, additive smithy travel, bow consumption/fallback, boss reset, map rewards, spawn counters and slot reload. See Evidence/C008-validation.json for the executed run and source/save preservation; do not substitute earlier C007 evidence. Windows player build and two-process restart evidence are recorded separately in the C008 handoff report.

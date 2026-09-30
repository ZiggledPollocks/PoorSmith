# Unity validation

## Field drop sprites and unified recipe map — 2026-10-01

Six gathering drop prefabs now use the same sprite selected by crafting's item ID/art fallback: wood, coal, stone, ore, branch and floating ore. The five small dedicated icons use SpriteRenderer Sliced sizing to retain their previous world footprint without changing transforms or colliders. The other six drops retain their distinct field sprites because crafting currently has only a shared leather placeholder for them. An isolated Unity 6000.3.11f1 Play Mode probe loaded all six changed prefabs and verified their sprite assets and the five preserved renderer sizes. It also opened the smithy recipe map and verified one full-screen graph, four resource roots, no category tabs or fog component, hidden undiscovered plank, and a newly discovered plank node and link. Evidence: `C:/Users/Master/Documents/Codex/recipe-sprite-probe-20261001.txt`. `dotnet build Assembly-CSharp.csproj --no-restore` passed with zero errors. Physical clicking, final Game View composition and a player build remain unverified.

## C039 forest/cave entrance fade — 2026-09-29

An isolated Unity 6000.3.11f1 Play Mode run passed 46 checks for the entrance plateau, retained roof clearance, cave/forest art and backdrop selection, fade-time input pause, both landing positions, camera confiner switching, restored time/input and legacy upper-mouth save placement. Four 1280×720 Game View captures of the forest approach/mouth and cave interior/return were rendered and inspected. Offline runtime and Editor C# compilation passed with zero errors. The copied Editor logged package-cache/Search diagnostics outside the passing probes. Actual keyboard traversal, intermediate fade frames, natural AI/resource behavior at the passage, live Editor and player build remain unverified. See [C039](Cycles/C039-field-entrance-fade.md) and [evidence](Evidence/C039-field-entrance.json).

## C031 fall-damage distance — 2026-09-28

An isolated `Assembly-CSharp.csproj` build passed with zero errors after syncing the changed PlayerMovement source. Saved scene values were checked: SampleScene, FieldMapStructureTest and NotionCampaign each retain safeFallHeight 6 and damagePerFallUnit 2, giving an effective safe fall of 8 with the added 2-unit grace. Unity Play Mode landing, the live Editor and a player build were not run.


## C028 town, forest and cave map UI — 2026-09-28

The isolated runtime assembly built with zero errors. Unity 6000.3.11f1 isolated Play Mode passed a saved town probe for the map button, explored smithy icon and indoor player marker at the town façade. A saved field probe passed the map button, visited forest and deep-cave cells, cave entrance/player markers, and opening/closing with time/input restoration. See [C028 evidence](Evidence/C028-town-field-map-ui.json). The copied Library logged package-cache and Unity Search startup exceptions; no map assertion or C# compiler error occurred. Rendered Game View appearance, physical mouse/keyboard operation and player build remain unverified.

## C027 slope and forest/cave transition — 2026-09-28

The modified runtime assembly compiled in an isolated copy with `dotnet build Assembly-CSharp.csproj` (zero errors), and the two installed script diffs passed `git diff --check`. A dedicated isolated Unity Play Mode probe for idle slope drift, uphill running/jump and entrance alpha positions was prepared, but the batch editor repeatedly failed to connect to Licensing Client and produced no probe result. No post-change physics, rendered transition, keyboard run or player build passed in this cycle. C025's zero resources/monsters and C026's injected-material scene round trip are prior evidence only; the ordinary gather→return→craft/equip→depart loop is not currently complete. See [C027 evidence](Evidence/C027-slope-entrance-loop-audit.json).

## C026 scene-local player state handoff — 2026-09-28

An isolated Unity 6000.3.11f1 Play Mode probe forced save-write failure for both town→field and field→town and verified that the source Player, bag/chest and previous durable save stayed unchanged. It also tested canceling a prepared transfer, restoring the previous save and blocking source autosave during handoff. A successful trip deliberately corrupted the disk snapshot before scene load; the destination restored from the memory handoff and repaired the durable save. Health, gold, a Medium-quality equipped sword, carried Wood, delivered Wood/Stone, input/time and the return position survived the round trip. A 105-unit inventory accepted exactly 21 × 5-unit items, then rejected the next item. The C025 forest wall contact/No/recontact/Yes fade and background/barrel probe passed again. See [C026 evidence](Evidence/C026-scene-player-handoff.json). Unity batch compilation had zero C# errors; copied Library package-cache/search startup exceptions remain unrelated to these passed assertions. The official saved-UI/inventory runner was attempted separately, but Unity Licensing Client connection failure prevented the probe from starting; the audit preserved source and user saves. Visible fade, physical keyboard travel and a player build remain unverified.

## C025 field boundary prompt, repeating backdrop and stationary barrels — 2026-09-28

An isolated Unity 6000.3.11f1 copy compiled and saved both scenes. Play Mode checked the repeating forest/cave background bounds against the camera viewport at six positions, found zero field resource/monster components and all three assimilation statues, then exercised the west forest wall contact prompt: No left the player in the field without immediately reopening; leaving and touching the wall again reopened it; Yes completed the existing fade travel to town. Four town façade barrel Rigidbody2D components remained static at their saved positions. See [C025 evidence](Evidence/C025-field-boundary-and-barrels.json). This does not verify rendered seams, physical keyboard movement, visual fade timing or a player build. The live editor was not reloaded.

## C024 playable gathering field — 2026-09-28

An isolated Unity 6000.3.11f1 copy saved and reloaded the field scene with a Cinemachine player-follow rig, two region camera bounds and five physical outer walls. Play Mode verified round-trip travel, field combat-tool activation and used-altar gating. Five forest/cave positions kept the full camera viewport within the active bounds and switched the background/HUD. A fatal hit applied the 4% gold and lost-bag penalty, returned to town and restored health, time and input. See [C024 evidence](Evidence/C024-playable-field.json). A batch-mode URP render request crashed before a screenshot; the non-rendering rerun passed. Manual keyboard, visible screen composition, full combat/resource scenarios and player build remain untested.

## C023 town/field bounds and travel confirmation — 2026-09-28

An isolated Unity 6000.3.11f1 copy saved both scene prompt instances and repaired town wall colliders. Play Mode verified the active Cinemachine town boundary, both wall centers/layers, exact Yes/No dialog, No cancellation in both scenes, Yes round trip through the existing fades, preserved gold, two Wood transferred to the chest and restored time/input state. See [C023 evidence](Evidence/C023-town-field-travel.json). Manual keyboard interaction, rendered UI/fade, all camera-edge compositions and a player build remain untested. Copied-Library package-cache/search startup exceptions occurred outside the passed assertions.

## C022 first-switchback art y-coordinate order — 2026-09-28

An isolated Unity 6000.3.11f1 copy saved/reloaded the field scene and verified lower/middle/corner art at tile y -12/-13/-14, with two one-way colliders moved to matching heights and six old art cells removed. Player-sized jump/landing/S-drop probes passed for both moved ledges. The actual saved player's movement and roll code then traversed right cave ground → corner → middle → lower → middle → corner → ground for 242 fixed physics frames, observing roll in both directions. Input state was injected in batch mode; physical keyboard feel, rendered Game View and build were not checked. See [C022 evidence](Evidence/C022-corner-foothold-y-order.json).

## C021 staggered first-switchback footholds — 2026-09-28

An isolated Unity 6000.3.11f1 copy saved/reloaded the field scene, checked two new x/y-offset one-way colliders and six collider-free PixelFantasy art cells, and passed jump/landing/S-drop physics on both. A second Play Mode probe used the authored player's movement and roll implementation with actual 2D physics for 212 fixed frames, reaching ground → lower → middle → existing corner ledge → middle → lower → ground. Roll state was observed on ascent and descent. Input state was injected in batch mode; physical keyboard hardware, visual Game View and player build remain untested. See [C021 evidence](Evidence/C021-staggered-corner-footholds.json).

## C020 field travel and preauthored visuals — 2026-09-28

An isolated Unity 6000.3.11f1 copy passed three-scene saved/reloaded authoring checks, first-switchback jump/landing/S-drop physics, field-platform Run → Walk → Idle selection, and a Play Mode trip from the saved town forest gate's confirmation through the field return gate. Two Wood transferred from the field inventory to the smithy chest; gold and UI/time state survived the round trip. Results: [C020 evidence](Evidence/C020-field-travel.json). These probes did not provide complete physical-keyboard traversal, rendered fade review or a player build. The copied Library reported package-cache/search startup exceptions; no C# compiler errors were found. The live editor was not reloaded.

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


## C009 authored town — 2026-09-27

Use Tools/TownValidation/Invoke-RuntimeValidation.ps1 for SampleScene town interactions. Fresh run 49c05a6df906 passed 31/31 with no errors; source, user saves and preferences preserved; no unexpected copied-input changes. Actual F input events exercise ForestIn/ForestOut/BlackSmith/pawnshop/Store. Checks cover fade opacity/input lock/arrival/fall reset, 5×5 sales and exact prices, exhausted buyback stock, shop filters/insufficient funds/bag capacity, no HUD shortcuts, smithy exit, night departure restriction and menu manual save. Settings entry uses the existing controller API; pointer clicks use button listeners. Three runtime renders were inspected at 1920×1080. This is not physical input/raycast, full terrain traversal, all-resolution or player-build evidence. See Evidence/C009-validation.json. The C008 template was adapted for asynchronous return and renamed shop rows; the full C008 suite was not rerun for this change.


## C010 town UI and map

Tools/TownValidation/Invoke-RuntimeValidation.ps1 now executes the C010 suite. Run d02acf8aae0a passed 46/46; no source/user persistence drift. It covers C009 routes/fades/trades/save access plus NPC farewell, owned bag tiers, living smithy/rack/workshop/left exit, day graphic wiring, real D-input town ground traversal, facility coming-soon, delivery whole stack and empty-target drag, single proceeds collection and E-input town inventory. Button listeners and pointer handlers are invoked programmatically; this is not a physical mouse raycast test. Eight renders at 1920×1080 were inspected. The capture adapter temporarily moves overlay canvases above world sorting layers and restores all canvas settings. No new build, full-map, all-resolution or restart-restoration evidence. C008/C009 evidence is historical. See Evidence/C010-validation.json.

Strict runner overall status remains failed: the isolated copy cleared the existing dynamic menu-font atlas. Semantic comparison confirmed only generated glyph/character/atlas caches changed; font configuration/references, source font and user persistence are intact. No validator protections were relaxed and the original failure record is retained. See C010-font-cache-review.json.


## C011 smithy integration

Tools/SmithyValidation/Invoke-RuntimeValidation.ps1 runs the C011 suite. Isolated Unity 6000.3.11f1 run 5d8e53fe5eb2: 38/38 Play Mode checks, zero runtime/compiler errors. Runtime and Editor offline compilation passed. Original SampleScene is byte-identical; all 5382 baseline inputs were checked before installation. Six captures inspected. Final verification reused the isolated Library after copying and hashing all source inputs; the preceding fresh import run is retained separately. Strict runner overall status remains FAILED because the isolated copy cleared the existing dynamic menu-font cache. Semantic review proves only generated cache data changed; the original font/source and user persistence are intact. The failed result is retained; no validator guard was relaxed. The probe uses real F/D input events; UI listeners and inventory methods are invoked programmatically. Actual mouse raycasts, every resolution, every crafting recipe, full combat and player build remain untested. Scene reload restores a test save, not a process-restart test.


## C012 alloy/shop checks

Run `Tools/AlloyValidation/Invoke-RuntimeValidation.ps1` for a fresh isolated test copy. Unity 6000.3.11f1 isolated Play Mode: 85/85 checks, zero runtime/compiler errors. Runtime and Editor offline compilation passed. Verified C011 physical smithy regression, real monster damage, blood/guard/body armor effects, fire refresh/coexistence/expiry, actual projectile collision and live burn Update, 0.6-health save/scene reload, shop filtering/selection/receipts/failed purchases/bag capacity and three farewell branches. Isolated Library reused with input hashes verified. Strict overall result remains FAILED: only generated glyph/character/atlas caches in two isolated dynamic font assets changed. Semantic cache review passed; source inputs and user persistence are preserved. No validation guard was relaxed. UI button listeners are invoked programmatically. Guard uses an Input System mouse-state event; projectile tests use the actual prefab collider. Burn timer boundary checks also call Tick deterministically. No exhaustive enemy AI, every resolution, physical mouse playthrough, process-restart save test or player build is claimed.

## C019 field platforms and background — 2026-09-28

The isolated Unity 6000.3.11f1 scene-save/reload checks passed for five distinct ledges, 20 collider-free visual tile cells, eight one-way polygons, one S-drop controller and forest/cave loops. A Play Mode player-sized capsule physics probe passed upward, horizontal, landing and S-drop cases, the existing upper landing, three background states and cave recycling. Forest/deep-cave captures were inspected. The probe manually advanced physics and moved the camera; full authored-player traversal, fade timing and a build were not run. The copied Library emitted package-cache/search startup exceptions despite passing results; see Evidence/C019-field-platforms.json.

## C029 player Aseprite visuals — 2026-09-28

In an isolated Unity 6000.3.11f1 copy, `PlayerVisualAuthoring.Build` completed and saved the three player scenes. `C029VerifyPlayerVisuals.Run` reopened each scene and sampled all nine Animator states, checking both sprite sources and matching frame names; all 27 state/scene samples passed. Unity editor C# compilation had no errors. The copied Library reported an unrelated Visual Scripting package-cache exception. A rendered Game View, real keyboard input, physics traversal with the new fitted collider and player build were not checked. See [C029 evidence](Evidence/C029-player-visuals.json).

## C032 field spawn and boundary checks — 2026-09-28

Targeted C# compilation passed with zero errors using installed Unity 6000.3.11f1 DLLs and the existing runtime assembly. Static checks confirmed five resource zones/68 targets, seven monster zones/12 targets with deer forest and all cave species cave, one golem target, all resource pickup references and cave-mask surface candidates. These do not establish that all targets spawn during Play or that Rigidbody2D, AI and knockback respect the boundary at every physics step. The isolated Unity editor could not connect to Licensing Client and did not enter Play Mode. Before claiming the gameplay loop works, run the manual field→town→smithy→field traversal and boundary/harvest checks described in [C032](Cycles/C032-field-spawns.md).

## C033 current field Play Mode results — 2026-09-28

The isolated non-batch Unity editor connected to Licensing Client and completed three Play Mode probes with code 0. Actual scene components produced 68/68 resources, 12/12 monsters, 12/12 correct initial forest/cave assignments and 12/12 forced cross-boundary returns. The real harvest/drop/automatic-pickup and two fade scene transfers delivered six wood to the town chest; crafting-domain operations produced and equipped a wood sword, and the returned field player's tool had damage 15. One crafted plank also returned in the player's bag. The tests directly invoked resource, crafting and travel methods in Play Mode. Do not treat them as physical keyboard/mouse, bed/night UI, natural AI knockback, timer-expiry, visual or build checks. The copied Library logged Visual Scripting package-cache and SearchDatabase startup exceptions; no clean-editor result is claimed. Details: [C033](Cycles/C033-field-play-validation.md), [evidence](Evidence/C033-field-play-validation.json).
# C034 quick-interaction hand animation — 2026-09-28

The isolated runtime C# project built with zero errors. In isolated SampleScene Play Mode, a valid tool-independent quick interaction called through the PlayerInteraction entry point completed exactly once; Animator entered `Attack` and its saved `PlayerHand` SpriteRenderer advanced through `playerhand_sp.aseprite` frames 24, 25 and 26. The first probe exposed an existing Animator evaluation-order bug; after the fix, the repeat passed. This does not verify hardware F/mouse input, rendered overlap, every UI interaction, or a player build. The copied editor still reports unrelated package-cache/search startup exceptions.
# C035 resource spawner links and scale — 2026-09-28

An isolated C# project build passed with zero errors. Unity 6000.3.11f1 Play Mode loaded the saved `FieldMapStructureTest` scene and populated all five resource zones to 68/68. Every spawned TreeZone resource held the scene `ItemDropSpawner`; directly harvesting a spawned tree and thicket generated six and four `ItemDropInteractable` instances respectively. Asset renderer heights were tree 3.36, thicket 3.40, and stone/coal/steel 3.3728 world units against the saved player height 2.216784. A first uniform-scale run reached only 13/18 TreeZone resources; reducing horizontal sprite scale while retaining the larger height restored 18/18. Physical interaction input, rendered sprite appearance, natural pickup and a player build remain unverified.

# C036 integrated tool selection and item art — 2026-09-28

The isolated `Assembly-CSharp` and `Assembly-CSharp-Editor` builds completed with zero errors. A static audit verified all 14 field resource/monster drop prefab references are covered by the 16 field-to-catalog links, and all 13 catalog sprite keys resolve in the shared art set. The eight installed files were guarded by original hashes and verified after installation; `git diff --check` passed. Two non-batch Unity 6000.3.11f1 attempts stopped before Play Mode because Licensing Client initialization failed. The prepared C036 buy/equip/save-reload/town-field probe did not run. See [C036 scope and manual check](Cycles/C036-integration.md); C033 remains previous-loop evidence only.

## C038 isolated licensing and scene validation — 2026-09-28

On this Windows host, a Unity Editor launched within Codex's filesystem sandbox could not connect to the already running Unity Licensing Client named IPC channel. The client answered other Editors; launching **only the isolated copied project** through the ordinary Windows process context connected immediately. Do not infer account-license expiration from the sandboxed `Connection to channel LicenseClient-Master refused` line alone, and do not reset the user's license or terminate their Editors for this condition. C038 Play Mode probes cover selected T2 tool persistence and town/field transfer, the gather→craft→field loop, and saved-scene hierarchy/camera/background/platform wiring. They use direct calls and do not replace input, Game View, build or clean-startup checks. See [C038](Cycles/C038-license-and-runtime-validation.md).

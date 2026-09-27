# C008 Notion campaign — 2026-09-27

The user authorized non-lava features and provisional missing balance. Start with Assets/Campaign/Scenes/NotionCampaign.unity. Day gathering → return → bed/nap → night crafting/equipping → sleep → next-morning delivery collection and departure. Existing scenes and unrelated edits are preserved. Notion was read-only. See Cycles/C008-notion-campaign.md for implemented scope, temporary values, remaining features and verification limits; Evidence/C008-validation.json records actual results.

# C007 smithing loop — 2026-09-27

Implemented the user-approved gathering / smithing / equipping / return loop on the saved batterMap base. Assimilation remains health and offerings heal. Notion was read-only. See Cycles/C007-smithing-loop.md for ownership, persistence, temporary combat values and limitations. Exact validation evidence is in the task output report; do not treat earlier C002/C003 results as validation of this change.

# Current handoff

## 2026-09-26 per-action player callbacks (C006)

PlayerInputHandler now has individual InputAction callback methods for 11 Player actions instead of one onActionTriggered dispatcher. Registration/removal is paired across OnEnable/OnDisable and rebound in Start after PlayerInput action initialization. C005 ToolScroll, modal gating, Hold-Interact timing and public consume methods remain. Offline runtime/Editor Roslyn compilation and static subscription symmetry passed; Unity import and Play Mode are not_run. See Cycles/C006-per-action-callbacks.md and Evidence/C006-per-action-callbacks.json.

## 2026-09-26 callback-only player input (C005)

PlayerInputHandler now uses one C# `PlayerInput.onActionTriggered` callback path, with no input polling in its Update. PlayerInteraction now gets attack press/hold/release exclusively from this handler. A `Player/ToolScroll` PassThrough Axis action binds `<Mouse>/scroll/y`. The Hold-based Interact action uses started to preserve immediate quick interaction; held Move/Sprint/Jump values survive modal UI transitions behind blocked public getters. Existing action entries, component/inputactions GUIDs and public consume APIs remain. Offline runtime/Editor compilation and static action diff passed. Unity import and Play Mode input behavior remain unverified. See Cycles/C005-callback-input.md and Evidence/C005-callback-input.json.

## 2026-09-26 project script refactor (C004)

All 215 Assets C# files inventoried (84 project-owned, 131 vendor/sample). The 84 project scripts were each dispositioned; 59 relocated into functional folders, 22 refactored, 17 new implementation/contract files added. Source and .meta hashes checked before/after installation, serialized declarations preserved, vendor sources unchanged. Runtime and Editor assembly offline compilation passed with zero errors. Unity import/Play Mode were not run. The standalone comparison harness failed with an unhandled .NET exception and supplies no gameplay evidence; it may explain the user-observed 0xe0434352 dialog. See Cycles/C004-script-architecture.md, Evidence/C004-script-refactor.json and the Korean full-file audit report in the Codex deliverables.

Next: verify Unity reimport, scene/prefab component references and behavior in a controlled editor session; do not count C003 runtime results as C004 validation. The previous auto-review rejection of a Unity retry due original source/save drift remains relevant. Preserve any user editor work while validating.

## 2026-09-26 movement mode refactor (C003)

Implemented the approved common movement contract, source/handle-based environment registration, and extraction of UpDraft, Flame and normal movement. Interface: Assets/JinHo/Scripts/Assets/IMovementMode.cs. Other new production classes: Assets/JinHo/Scripts/player/Movement. Existing PlayerMovement and UpDraftZone components retain their paths, GUIDs and all 49 serialized fields; no scene/prefab migration. Existing roll direction changes and commented grounded-roll guard preserved.

PlayerMovement selects IMovementMode and applies MovementCommand. Environment membership policies handle jump/fall suppression and gravity independently of roll/knockback. Flame priority and newest equal-priority UpDraft selection remain; UpDraft exit preserves Y velocity. Zone contacts track multiple colliders, cleanup and re-enable. Physics remains velocity-based; AddForce support is only a command-contract extension, not a gameplay conversion. Roll, ground sensing, input timing, fall damage and CharacterPhysics2D remain in their existing scope.

Validation: Unity 6000.3.11f1 isolated compilation and Play Mode assertions passed 300/300, including 64 movement checks, plus 17 runner guards. Overall run is BLOCKED: source drift (SampleScene and dynamic font) and user persistence drift were detected. Do not claim preservation or an overall pass. A separate live original-project Unity process was observed; the probe's marker confirms its separate project/persistence namespace, but the writer of the drift is not proven. Auto-review rejected a retry; no retry ran and no original changes were restored. Evidence: Docs/Evidence/C003-movement-results.json; full run: C:/Users/Master/Documents/Codex/UV/8aca78039e5d/evidence. Movement checks use deterministic calculations and synchronous real-component/callback integration; actual keyboard traversal, collision-driven trigger traversal, visual feel and player builds remain unverified.

Next: obtain user approval for the rejected validation retry, with the original project's editing/play/build activity paused, to establish an unchanged baseline. Also manually traverse saved UpDraft/Flame regions, verify jump/coyote/buffer feel, roll while overlapping environments, then portal and knockback transitions. See Cycles/C003-movement-modes.md. Further force-based movement or extracting roll/ground/fall systems is outside this implemented scope.


## 2026-09-26 roll direction update

PlayerMovement now chooses roll direction from MoveInput.x (threshold 0.01); with no horizontal input it uses the animation renderer's flipX. Pointer position and rigidbody velocity no longer select roll direction. Existing local changes, including the commented grounded-roll guard, were preserved. No scene or Inspector migration is needed.

Validation: isolated Unity 6000.3.11f1 Windows64 editor compilation passed; no compiler/import errors and original inputs preserved. Evidence: C:/Users/Master/Documents/Codex/UV/f8b7942dc7aa/evidence/result.json. Play Mode and player build were not run. Remaining manual check: roll with left/right input while the mouse is on the opposite side, then release horizontal input and roll while facing each direction.


Updated: 2026-09-23. Latest work: C002 M2 runtime harness and first M3 inventory checks implemented/executed; overall result passed. C001 and M1 remain complete.

## Scope and state

The user approved Docs in the repository and a new Wiki beneath the existing Obsidian batterground folder, with existing notes preserved. First learning coverage includes all locally identified project history rather than only inventory.

C001 is complete: documents are installed and content/preservation checks passed. See Evidence/installation-report.md. C002 M1 subsequently added Tools/Validation and verified isolated editor compilation plus deliberate-failure detection. See Validation.md. C002 M2/M3 subsequently executed Play Mode on a saved copy; latest result passed. No gameplay code was changed and no mutating UI/resource generators were invoked.

## Source baseline

Branch: JinHo_1. HEAD: f21ee84cbb3c9f1f220a901c76a1ec361fcef059. The working tree already contained changes in:

- Assets/JinHo/Scripts/player/PlayerInputHandler.cs
- Assets/SettingsMenuUI/Editor/SettingsMenuUIBuilder.cs
- Assets/SettingsMenuUI/Fonts/NanumGothic Dynamic SDF.asset
- Assets/SettingsMenuUI/Scripts/ControlSettingsController.cs
- Assets/SettingsMenuUI/Scripts/GameState.cs

Preserve these changes. The source manifest describes the working tree, not HEAD alone. Recheck branch and status before future work; this is not authorization to commit or push.

## Open matters and next action

Final operational agreements and M1/M2/M3 are approved. M2/M3 implementation and execution are recorded in Cycles/C002-runtime-validation.md and Evidence/M2-M3-validation-results.json. Latest overall status: passed; assertions 236/236, M3 inventory 10/10, guard tests 17 passed. Original saved inputs and user persistence preserved: True / True.

Use Tools/Validation/Invoke-RuntimeValidation.ps1. Do not invoke the legacy UIIntegrationSmokeTest for the current workflow. First trial encountered an editor-internal SearchDatabase exception; the subsequent run uses a pre-Play quiet interval. Retain this history, and do not equate successful UI assertions alone with an error-free overall run.

Next: use this validation cycle for the next requested feature/fix, adding only affected-neighbor gameplay scenarios. M3 currently covers inventory model boundaries, not the entire game. No new game rule is pending merely to adopt the harness. Product fixes still require their proper scope/design agreement. Static candidates remain offering bulk-return failure handling, save-load failure policy, assimilation zero/death semantics, boss/portal re-entry and physical pickup/drop. None is authorized as a product change by these test approvals.

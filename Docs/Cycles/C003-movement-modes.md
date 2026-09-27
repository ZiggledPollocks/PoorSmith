# C003: movement modes and environment registration

Approved scope (2026-09-26): introduce a common movement contract, an environment registry, and extract UpDraft, Flame, and normal movement while preserving gameplay. Put interfaces in Assets/JinHo/Scripts/Assets and other new production code in Assets/JinHo/Scripts/player/Movement. Preserve existing serialized fields, script GUIDs, unsaved scenes, and unrelated local changes.

## Implementation boundaries

- IMovementMode.Calculate receives a state snapshot and returns a MovementCommand. PlayerMovement applies gravity, per-axis velocity overrides, and optional forces. Existing modes remain velocity-based; force-based UpDraft is not implemented in this cycle.
- NormalMovementMode owns acceleration/deceleration, direction reversal, jump velocity and gravity calculations. PlayerMovement retains input consumption, jump/coyote timers, rolling, ground probes, fall-damage callbacks, and public animation state.
- MovementModeRegistry uses source identity and registry-scoped handles. Higher priority wins; newest equal-priority registration wins; repeated stay does not refresh ordering. Policies combine actual environment membership independently of roll/knockback overrides.
- Flame has priority 200; UpDraft 100; normal movement is the fallback. FlameContactSensor adapts existing layer triggers without scene migration. MovementZoneContacts tracks player colliders and removes a registration only when its final contact exits; disabled/destroyed sources and re-enabled players are handled.
- UpDraftZone retains its serialized zone settings, target-height formulas, and existing public height/speed accessors. It now supplies a mode through the generic registration API. PlayerMovement.EnterUpDraft/ExitUpDraft are replaced by RegisterEnvironmentMode/UnregisterEnvironmentMode; all identified production callers are migrated.
- UpDraft entry clears jump buffers and fall tracking, disables gravity without changing velocity. Final environment exit restores base gravity and preserves Y velocity. Overlapping Flame/UpDraft keeps gravity disabled. Roll and knockback ordering remains unchanged, including the pre-existing commented grounded-roll guard.
- Existing Inspector fields stay in PlayerMovement; mutable runtime settings refresh from them. No scene/prefab serialization or reference migration is required. Existing animation/portal/boss wind dependencies remain supported.
- CharacterPhysics2D and portal velocity writes remain separate existing authorities. This cycle does not redesign combat, portals, saved data, or multi-source wind combination.

## Validation

Use Tools/Validation/Invoke-RuntimeValidation.ps1. MovementCases.cs.txt is injected only into the disposable project and runs beside the saved UI/inventory checks. Cases cover numeric movement outcomes, environment priority/identity/cleanup, real component command application, trigger callback integration, overlap, roll, knockback, disable/re-enable, and unchanged upward exit momentum. Direct callback tests do not constitute physical keyboard/trigger traversal or visual validation. Report actual run evidence in CURRENT and Validation after execution.

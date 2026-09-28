# C025 field boundary travel and stationary town barrels — 2026-09-28

## Result

`FieldMapStructureTest` retains its authored terrain, platforms, forest/cave camera bounds and region-based repeating backgrounds. The forest background root moved upward by 2 world units after a Play Mode viewport-bounds check found a narrow uncovered strip at the camera top. The existing `InfiniteBackground2D` forest sections and `InfiniteBackground2DXY` cave sections still recycle with the camera, as in `SampleScene`; no new background art or resource prefab was introduced.

The forest's saved west wall is the return point. `FieldSceneReturnGate` now listens for player collision on that solid Ground-layer wall rather than implementing `IInteractable`. It opens the existing `FieldTravelPrompt` once per contact. No cancels without reopening until the player leaves the wall; a new contact can prompt again. Yes calls the existing saved-progress/material-transfer and fade travel to `SampleScene`. The separate `Forest Return To Town` quick-interaction object was removed.

The field scene's 30 tree, thicket, ore and animal/monster instances were removed. Three `AssissZone` statues were reparented under `Field Assimilation Statues` and kept with their saved identities and positions. No monster/resource scripts or prefabs were deleted from the project, and `SampleScene`'s field gate was not changed.

Four town façade `PF Village Props - Barrel` instances had disabled polygon colliders but simulated dynamic Rigidbody2D bodies with gravity. They are decorative, so their saved `SampleScene` instances now have static bodies and zero gravity. This preserves their authored position during Play without changing the shared prefab or the smithy barrel, which was already unsimulated.

## Verification and limits

The isolated Unity 6000.3.11f1 copy compiled the runtime code, saved and reloaded both scenes, and passed a Play Mode probe at three forest and three cave camera positions: the active repeating sections covered the full camera viewport in bounds, with three forest and nine cave runtime sections. The same probe confirmed zero scene monster/resource components, three retained statues, wall collision opening the exact Korean Yes/No prompt, No cancellation without immediate repeat, recontact opening it again, Yes starting the fade and loading town, and four barrel positions unchanged after Play frames. See [evidence](../Evidence/C025-field-boundary-and-barrels.json).

Bounds coverage is a geometry assertion, not a rendered Game View inspection. Visible seams, fade appearance, physical keyboard travel and a player build remain unverified. The original Unity editor was not reloaded; protect any unsaved editor changes before reopening the saved scenes. No Notion text was modified, and no commit or push was made.

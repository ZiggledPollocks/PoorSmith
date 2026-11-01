# C019 one-way field platforms and camera backgrounds — 2026-09-28

## Behavior

`FieldMapStructureTest.unity` has five separate, horizontally joined hairpin ledges (20 cave tile cells total). Each has its own `PolygonCollider2D` and `PlatformEffector2D` on Ground. The underlying visual tile uses the same cave artwork with its tile collision disabled, so it does not create an invisible side wall. The three previously solid smooth shortcut/landing polygons use the same one-way setting. Other terrain remains solid. A player can move through a ledge's side or rise through its underside, then land on its top. Pressing S while standing on a marked platform temporarily ignores only that platform and starts a downward drop; collision returns after the player clears its local underside or moves outside it. Modal UI blocks the drop input.

The existing forest background repeats horizontally with the camera. The cave background repeats around the camera in both axes during Play while retaining the authored tiles in edit mode. The entrance's existing fade now also regards y <= -10 as cave in this scene, so the leftward deep switchback does not reveal the forest. The added depth boundary is opt-in and does not alter `SampleScene`'s default behavior.

Only this field test scene uses the drop controller and depth boundary. No Notion content, other scene, prefab, warp stone, or build-scene list was changed.

## Verification and limits

An isolated Unity 6000.3.11f1 project saved/reloaded the scene and passed checks for 5 separate polygons, 20 collider-free visual cells, 8 one-way platforms, one player drop controller and both background loops. A Play Mode physics probe with a player-sized capsule crossed a new ledge from below and horizontally, landed from above, dropped with a real Input System S event, and rose/landed through the existing Upper item landing. It also checked forest, entrance cave and deep-left cave background selection and cave tile recycling. Forest/deep-cave screenshots were visually inspected. The probe simulated physics steps and positioned the camera directly; complete keyboard-driven traversal with the authored player, fade timing, animal AI, player build, and editor-open scene state remain unverified. Unity's copied Library emitted package-cache/search startup exceptions, but the validation results passed and no C# compiler errors were found.

Because the original Unity editor was already open, validation used a separate project copy. The original saved scene and shared background script hashes were checked immediately before installing the ten validated source files. The live editor was not reloaded; preserve any unsaved editor edits before reopening the saved scene.

# C016 SampleScene player in field-map test — 2026-09-28

## Purpose and implementation

`Assets/Scenes/FieldMapStructureTest.unity` contains a test-only copy of the saved `SampleScene` root `player` hierarchy. All 18 saved components and children, Input Actions, movement/animation/physics components and self references are retained. It starts just inside the cave near x = 46.37, y = 0.66, above a flat corridor floor. `SampleScene` itself is unchanged.

The copy's `PlayerSaveSystem` component is disabled so testing this standalone map does not automatically load or write normal player persistence. The old `FieldMapPreviewCamera` is disabled in this scene, since its WASD/arrow navigation competes with player input. The new `FieldMapTestPlayerCamera` follows the copy, keeps the existing x = -65…115, y = -62…19 map bounds, and retains mouse-wheel zoom. The camera script is only attached to this test scene.

## Verification and limits

An isolated Unity 6000.3.11f1 scene-build run saved the player and camera wiring. In isolated Play Mode, the copied player rendered, all 18 saved components and Input Actions were present, the runtime fitted CapsuleCollider2D measured approximately 1.43 × 2.67 world units, the spawn did not overlap the cave wall, and manually invoked camera follow centered on the player. The test scene and `SampleScene` stayed byte-identical throughout this Play verification. A screenshot was captured.

Unity batch-mode Play entered but its game time remained 0, so automated walk, jump and roll input did not advance. Those actual control behaviors still require opening this scene in an interactive Editor and pressing Play. The test-only persistence component must remain disabled. The map's earlier offline geometric reachability model is not a physical player playthrough. See `Docs/Evidence/C016-field-test-player.json`.

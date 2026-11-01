# C013 field map structure test — 2026-09-28

> Superseded cave geometry: C014 replaces the C013 solid cave routes with empty traversable corridors and colliding white-region walls. This page remains the historical C013 record.


## Intent and reference

The read-only Notion `채집 필드 수정사항` map sketch shows a forest surface at the left, a cave entrance at the right, and one connected descending cave system with branches and loops. This implementation is a geometry reference scene only. It reuses the saved project forest backdrop, cave backdrop, cave entrance art, forest ground tiles and cave rock tile. Lava is outside this scope.

## Implemented

- `Assets/Scenes/FieldMapStructureTest.unity`: independent test scene with 12 named, connected terrain routes and PolygonCollider2D shapes, five existing forest backdrop sections, 49 existing cave backdrop sections and the existing cave entrance art.
- `Assets/Campaign/FieldMapPreviewCamera.cs`: WASD / arrow pan, mouse-wheel zoom and rectangle clamping. No gameplay objects, player, resources, animals, monsters, chests, UI, warps or interactables are in the test scene.
- The scene is intentionally excluded from Build Settings. Open it explicitly in the Editor, press Play, then pan and zoom. `SampleScene` and the current build scene selection are unchanged.

## Verification and limits

Unity 6000.3.11f1 isolated Play Mode passed: 12 routes, 2,352 occupied terrain cells, five forest backdrop copies, 49 cave backdrop copies and zero gameplay MonoBehaviours. Both top and bottom camera clamps were exercised. Five Play captures were inspected, including one full overview. No runtime/compiler errors remain in the final run. The live original editor was not reloaded, and no player traversal or full physics playthrough was performed; path accessibility should be evaluated when a gameplay player is added later. Notion content was read only.

See `Docs/Evidence/C013-field-map-structure.json` and the task outputs for screenshots.

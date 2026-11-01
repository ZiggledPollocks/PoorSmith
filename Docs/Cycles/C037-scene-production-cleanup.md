# C037 production scene cleanup — 2026-09-28

Scope: `Assets/Scenes/SampleScene.unity` (the town scene referred to as SimpleScene), `Assets/Scenes/FieldMapStructureTest.unity`, and the saved `TownSceneIntegration` references. The user confirmed both scenes were saved before replacement. This change did not touch Notion, other scenes, prefabs, player assets, or game-save data.

## Final scene ownership

- `SampleScene`: retained `Town`, its authored ground/shop facades, `SmithyInterior` prefab and support bounds, player, campaign systems, cameras and lighting. Removed the obsolete root `Forest`, `Cave`, `Storm`, `CaveBackGroundGroup`, old gathering `Grid` (including its child prefab instance), `SpawnManager` and `AssissZone`. The town's own `TownPresentation/TownGrid/TownGround` is separate and remains intact. New roots group the campaign systems, actors, cameras/lighting and smithy boundary supports.
- `FieldMapStructureTest`: retained every pre-existing GameObject and its component IDs. New roots group terrain/boundaries, backgrounds, spawn systems, interactions, actors and cameras. Resource and creature zones have nested parents. The two stone zones and three slope colliders now have distinct role names; their components and saved local positions are unchanged.
- `TownSceneIntegration` tolerates the removed legacy `ForestOut` reference. `forestOut` is now null in the town scene, and the campaign's fallback field spawn uses `forestIn`; actual town→field travel still uses `FieldSceneTravel.FieldArrival`. The Cinemachine confiner's saved shape now points to `Town/Bounds/camerBounds`, not the deleted forest shape.

## Safety and performance

The old town/field duplicate was primarily a serialized old gathering Tilemap. Removing it reduced `SampleScene.unity` from 5,983,584 to 496,415 bytes. This is a scene serialization/import-size improvement; no frame-rate claim is made. In the field, the 69 non-template cave backdrop sections are now under `Cave Background Preview (Editor Only)`. They remain visible for scene authoring, while the `EditorOnly` tag strips the preview parent from player builds. The one direct cave section remains the serialized runtime template, and `FieldCaveBackgroundRuntime`/`InfiniteBackground2DXY` still use it for the 3×3 camera-following backdrop. Runtime improvement must be measured in a player build.

Static inspection found no duplicate Rigidbody2D, 2D collider, SpriteRenderer, Tilemap or TilemapRenderer of the same type on a single saved GameObject. The existing one-way platform collider, PlatformEffector2D and ground collision serve separate movement functions, so they were retained. Repeated town facade art and cave section art are prefab candidates, but conversion would change prefab boundaries/overrides and has no demonstrated runtime benefit here. The smithy interior is already a prefab. No automatic prefab conversion was performed.

## Verification

An isolated copy and hash-guarded backups preceded installation. Structural checks compared old and new YAML file IDs, component IDs and every preserved GameObject's local position, rotation and scale: town 102 preserved, 66 legacy GameObjects removed, four grouping parents added; field all 363 preserved, nine grouping parents added. No newly dangling local fileID references were found. The field cave has exactly 69 editor preview sections. The static evidence is `Docs/Evidence/C037-scene-structure.json`.

The first-party runtime and Editor assemblies passed an offline Unity-reference Roslyn compilation with zero errors (existing warnings remain). Unity 6000.3.11f1 was launched against the isolated copy for an Editor authoring pass and a subsequent Play Mode integration probe. Both were blocked before scene/Play execution by the local Licensing Client refusing its IPC channel; the authoring command was not used to install files. A generated `.csproj` build was inconclusive because Unity had not produced the package assembly references after startup failed, but the separate Roslyn check succeeded. Scene load, Game View, scene travel, input/physics, save round-trip, player-build and visual checks for this cleanup remain unverified. The earlier C033 Play Mode result predates this scene cleanup and does not validate it.

When Unity licensing works, open each scene, inspect the grouped Hierarchy and missing-script/Inspector warnings, enter Play Mode, then verify town↔field fade and state transfer, smithy entry/exit, cave background movement, one-way slopes, spawning, harvest→craft→equip→re-enter loop, save/reload and a player build. The C036 integration probe should be rerun against these saved scenes.

## C038 follow-up

The saved scenes subsequently loaded in isolated Play Mode. A 25-assertion scene probe confirmed the hierarchy groups, absent old town field/wind roots, both camera bounds, smithy prefab, disabled cave authoring preview, three one-way slopes and resource-zone wiring. The gather→craft→return loop also completed. See `C038-license-and-runtime-validation.md` for the corrected selected-tool bug and remaining visual/input/build limits; the earlier licensing block above describes the C037-time attempt only.

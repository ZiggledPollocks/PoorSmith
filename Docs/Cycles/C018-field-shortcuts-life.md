# C018 field shortcuts and prefab placement — 2026-09-28

## Scope

Only `Assets/Scenes/FieldMapStructureTest.unity` changes. The upper item-marked area gets a short approach/landing. Two diagonal passages cut through the rock between the existing cave switchbacks near the moss-slime and stone-golem regions. Each passage uses an instance of the existing `PixelFantasySlopeTilemap.prefab`: its tilemap displays PixelFantasy cave ledge tiles, while a separate polygon provides a smooth, solid Ground-layer slope. The tilemap's stepped collider is disabled. No warp stone, warp interaction, unlock item, or new game rules are added.

The forest now has tree, thicket and deer prefab instances. The cave has stone, coal, iron, bat, moss-slime and stone-golem prefab instances plus three `AssissZone` statue prefab instances with distinct stable IDs. All 33 resource/creature/statue instances are saved under `Field Fauna and Resources` and grouped by forest or cave. The statues are in the cave only. The cave-mouth art has the same `CaveEntranceBackgroundTransition` component used by `SampleScene`, wired to the saved forest and cave background groups and the test player. The player copy continues to have its save component disabled. `SampleScene`, the original prefabs and Build Settings remain unchanged.

Before installation, the live saved test scene gained a `UniversalAdditionalCameraData` component and the cave entrance art moved to y = 3.37. Both saved edits were merged into the validated result rather than overwritten.

## Verification and limits

In an isolated Unity 6000.3.11f1 copy, Play Mode checked all 181×82 cave-mask cells against wall collision, verified 2,119 PixelFantasy facing tiles, sampled rock collision, checked all 3 smooth slope surfaces and more than 90 player-sized capsule placements along them, inspected all 33 prefab links, confirmed 3 cave-only altars can resolve the player's offering UI, and tested immediate forest/cave background selection at the entrance. Six screenshots were captured and inspected. The scene and `SampleScene` remained byte-identical during verification. Unity batch Play does not advance game time in this validation setup, so actual input-driven traversal, animal AI, gradual background fading and a player build are not verified. The earlier offline reachability result does not apply to the new crosscuts because their smooth slope colliders are separate from the binary terrain mask.

Open the scene directly in Unity, press Play and walk from the forest through the cave mouth. Test the upper landing and both middle crossings in both directions; the saved player starts just inside the cave. Confirm a statue opens the offering UI and that the forest background returns when walking out. This scene is a field test, not a replacement for `SampleScene`.

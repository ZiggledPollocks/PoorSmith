# C017 branched PixelFantasy cave — 2026-09-28

## Behavior and scene structure

`Assets/Scenes/FieldMapStructureTest.unity` is an isolated geometry test. The saved `SampleScene` player is copied into it, with `PlayerSaveSystem` disabled on the copy and a bounded follow camera. `SampleScene`, the forest terrain, and gameplay scenes remain unchanged. The cave entrance starts at the same upper-left mouth, then follows broad upper, middle and deep switchbacks with a relief branch and lower-left pocket inspired by the gray cave region in the supplied sketch. The white region is solid wall; gray passages are empty and reveal the existing cave background. Bounds are x = -65…115 and y = -62…19.

The 181×82 binary corridor mask is the geometry source. A dark transparent/opaque sprite covers only finite far rock mass. The player-facing wall edge is a visual-only Tilemap with 2,162 tiles from `PixelFantasy_Caves_1.0`; the separate invisible Tilemap has 7,127 exact-cell colliding wall tiles. This keeps palette art from enlarging collision. Forest space is excluded from both cave Tilemaps. The copy of the saved player has 18 components and camera follow; old keyboard preview navigation is disabled in this scene so it cannot consume movement input.

## Verification

An isolated Unity 6000.3.11f1 Play Mode run matched all 181×82 mask cells against collision, checked the palette assets and visual/collision separation, sampled 2D physics, captured six views and compared the forest capture byte-for-byte to the previous image. The `SampleScene` file was byte-identical before and after. Another isolated Play run checked the player Input Actions, runtime fitted collider (~1.43×2.67 units), non-overlapping spawn and camera follow. An offline conservative movement-envelope model found 559/559 grounded positions reachable from the entrance with measured jump (~3.06 units) and roll (~3.75 units) parameters. That model is an approximation; batch Play time did not advance, so actual keyboard walking, jumping and rolling still need an interactive Editor playthrough. No player build or commit/push was run.

Open `FieldMapStructureTest.unity` directly and press Play. Use the copied player to inspect the upper, middle and deep routes. Its save component must remain disabled. The old preview camera can be re-enabled only when the follow camera is disabled.

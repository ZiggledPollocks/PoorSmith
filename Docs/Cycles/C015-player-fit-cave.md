# C015 player-fit cave — 2026-09-28

## Scope and measured movement

This updates only the cave in `Assets/Scenes/FieldMapStructureTest.unity`; the forest, `SampleScene`, objects and gameplay rules remain outside this map-only scene. The saved `SampleScene` player has an approximately 1.75 × 2.70 world-unit CapsuleCollider2D. Its jump rise is about 3.06 units at the default gravity and its roll covers about 3.75 units horizontally; rolling does not shrink the collider. These measured dimensions, rather than the pixel proportions of the hand-drawn sketch, set the cave clearance.

## Map construction

The overly broad C014 cave network is replaced by three connected switchback bands: upper cave, middle cave and deep cave. The authored centerline retains the sketch's descending left/right flow. A 3.5-unit radius generally gives a seven-cell cross-section; short footholds at the left hairpin bridge the one merged chamber. The mask contains 2,827 open corridor cells, down from C014's 10,774. The scene's finite bounds are x = -65…115 and y = -46…19.

The existing cave background shows through transparent corridor pixels. A single `PlayerFitCaveWall.png` sprite covers the far impassable mass with a narrow brown rock lip, replacing C014's 20,453 repeated visible rock tiles. An invisible Tilemap with exact one-cell colliders blocks the white-wall portion inside the bounded scene. Above-ground forest map and art are preserved. `Tools/MapReference/PreparePlayerFitCave.py` regenerates the binary mask and wall sprite; `BuildPlayerFitCave.cs.txt` records the isolated scene migration.

## Verification and limits

A conservative offline reachability approximation using the measured player width, height, run speed, jump speed, gravity, and roll distance found all 368 generated grounded positions reachable from the mouth. This is a geometry calculation, not a Unity player playthrough. In an isolated Unity 6000.3.11f1 Play Mode run, all 2,827 corridor cells and 5,899 colliding wall cells matched the mask; sampled physics points, camera bounds and six visual captures passed. The forest capture matched C013 byte for byte. `SampleScene` stayed byte-identical. The scene still intentionally has no player, resources, creatures or gameplay transitions. Actual manual movement with the saved player and a player build remain unverified. See `Docs/Evidence/C015-player-fit-cave.json`.

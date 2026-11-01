# C014 inverse cave mask — 2026-09-28

## Correction

The user clarified that the gray regions in `1000001699.jpg` are traversable cave voids showing the existing cave backdrop; white regions are impassable rock. The C013 cave routes had this relationship reversed. Only the cave portion of the independent `FieldMapStructureTest` scene is changed. The forest Tilemap and backdrop remain intact.

## Implementation

The attached sketch was sampled into a 261×143 cell mask, small label/prop marks were removed, and the main connected gray cave network was widened two cells on each exposed edge per the follow-up request. The resulting test scene has 10,774 empty corridor cells and 20,453 visible cave wall cells. Wall fill stops at the preview camera's finite map bounds, leaving the gray cells empty so the existing cave backdrop shows through. The preview camera begins in the cave and clamps within those bounds.

The reused `CaveRock111` tile has a 6.25 transform scale, so its built-in collider overlaps open cells. Rendering and collision are separated: visible rock uses that existing tile; an invisible Tilemap with `CaveWallCollisionTile.asset` has exact one-cell Grid colliders for the same wall cells. The forest above-ground region is excluded from wall generation.

## Verification and limits

In an isolated Unity 6000.3.11f1 Play Mode run, every mapped cell matched the sketch mask, white wall sample points blocked and gray corridor sample points remained open, the camera clamped at both extremes, and six captures completed. The pre-change and post-change forest captures were byte-identical. No player or resource objects were added; actual player traversal, movement on steep slopes, and player build remain untested. `SampleScene`, build settings and Notion were not changed. See Evidence/C014-inverse-cave-mask.json and Tools/MapReference.

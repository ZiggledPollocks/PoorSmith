# Cave map reference source

`cave-corridor-mask.png` is the 261×143 binary mask derived from the user-provided `1000001699.jpg` sketch. White mask pixels mean an open cave corridor; black mask pixels mean a solid cave wall. The above-ground forest is protected separately by the scene builder.

`PrepareCaveMask.py <source-sketch.jpg>` recreates the mask using the documented fixed coordinate mapping and widens gray passages by two Unity cells on each edge. It requires Python with Pillow and NumPy. Run it in an isolated working directory, not against the live Unity scene.

`CarveCaveFromSketch.cs.txt` is an Editor-only scene migration source guarded for the `BatterMapValidation` isolated project. It consumes a `-caveMask` path and saves `Assets/Scenes/FieldMapStructureTest.unity` in that isolated copy. Do not rerun it on a user-edited scene; preserve changes and compare the source scene first.

## C015 player-fit cave

`PreparePlayerFitCave.py` generates `player-fit-corridor-mask.png` and `PlayerFitCaveWall.png` from an authored player-scale switchback based on the same supplied cave sketch. It needs Python, Pillow and NumPy. White mask pixels are empty cave space; black pixels receive exact-cell collision, except the protected forest. The wall PNG is a single imported sprite, transparent in corridors and opaque in the finite rock mass. `BuildPlayerFitCave.cs.txt` documents the isolated Unity migration. Its batch-only guard requires the `BatterMapValidation` copy. Do not rerun it against user-edited scenes.

## C017 branched PixelFantasy cave

`PrepareBranchedPaletteCave.py` records the player-scale branching corridor mask. `branched-cave-corridor-mask.png` is the generated 181×82 geometry; white means open space. `BuildBranchedPaletteCave.cs.txt` is a one-time isolated Unity migration that requires the saved test player and the existing PixelFantasy cave palette. Do not rerun it directly on a user-edited scene.

## C018 field crossings

`PrepareCrosscutCave.py` regenerates the mask with moss-slime and stone-golem crosscuts. `crosscut-cave-corridor-mask.png` is the 181×82 open-space mask. `PlaceFieldLifeAndSlopes.cs.txt` records the isolated scene placement of slope prefab instances, resource/creature/altar prefabs and SampleScene entrance background wiring. It must not be run directly on an edited live scene.

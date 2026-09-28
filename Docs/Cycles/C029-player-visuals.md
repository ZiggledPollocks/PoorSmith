# C029 player Aseprite visuals — 2026-09-28

`Assets/source/Player/player_sp.aseprite` and `playerhand_sp.aseprite` each contain 29 frames and four tagged animations: `idle` (0–8), `walk` (9–16), `run` (17–23), and `attack` (24–28). The live player previously displayed `Hero and Opponents`' walk sprite while playing the `Warrior free set` controller.

`Tools/Player/Build Player Visuals` creates paired body/hand animation clips and a controller with the state names expected by `PlayerAnimationController`. The root SpriteRenderer displays the body, while a child `PlayerHand` renderer uses the matching hand frame at a higher sorting order. The controller and first frame are saved in `SampleScene`, `FieldMapStructureTest`, and `NotionCampaign`; the existing physical player, controls, camera and scene state remain in place. `PlayerAnimationController` mirrors the hand when the player faces left.

The Aseprite sources do not contain jump, fall, roll, hurt or death art. The controller uses the new idle frame for jump/fall/hurt/death and the new run frames for roll until dedicated art is supplied. These are visual placeholders; the existing gameplay states and timings are unchanged. Attack is a non-looping clip.

Verification: an isolated Unity 6000.3.11f1 editor compiled the project, saved and reopened all three scenes, and sampled all nine controller states in each. Every sample resolved both body and hand sprites from the respective source files with matching frame names. See [evidence](../Evidence/C029-player-visuals.json). A rendered Game View, physical keyboard movement and a player build were not checked. The copied Library emitted an unrelated Visual Scripting package-cache exception during startup.

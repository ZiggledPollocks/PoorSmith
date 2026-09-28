# C020 field travel and authored scene visuals — 2026-09-28

## Behavior

`FieldMapStructureTest` adds a separate three-cell, one-way foothold at the first far-right cave switchback. The new visible PixelFantasy cave tiles have no Tilemap collision; a dedicated polygon and effector supply top-only collision and S drop. On marked field platforms, movement input determines Idle, Walk or Run, so releasing Shift on a slope changes Run to Walk even if physics retains small horizontal velocity. The field scene uses a saved Walk animation and controller; the original Warrior controller remains untouched.

The authored ForestIn in `SampleScene` now confirms departure to `FieldMapStructureTest`. A saved Forest Return To Town object handles quick interaction back to `SampleScene`. Travel saves progress, transfers carried items to the field and collected materials to the smithy chest, loads the target scene with a black fade, restores the player's state and re-enables gameplay. `FieldMapStructureTest` is enabled in Build Settings. The separate `NotionCampaign` gate keeps its previous in-scene teleport behavior.

`SampleScene` and `NotionCampaign` now contain saved instances of the existing SmithyInterior prefab. Each active scene's player has a saved SpriteRenderer, Animator and PlayerAnimationController, with its controller assigned before Play. Runtime smithy prefab creation and runtime player animation-component creation are removed. The smithy window tint and UI remain state-driven; room geometry and player animation assets are authored ahead of Play.

## Verification and limits

An isolated Unity 6000.3.11f1 editor saved and reloaded all three scenes and verified their authored components/interiors. Play Mode verified the first corner's jump, landing and S drop with a player-sized physics probe; marked-platform animation changed Run → Walk → Idle as input was released. Another Play Mode run used the town forest gate's confirmation button, entered the field, collected two Wood, used the return gate and verified Wood in the smithy chest, preserved gold, arrival positions, authored visual components and restored input/time state. The isolated project used a separate save namespace. Compilation passed without C# errors. Copied Library startup emitted package-cache/search exceptions unrelated to the completed assertions.

Full keyboard-driven traversal over every cave shortcut, actual input-device Shift release, fade appearance/timing, in-editor Game View visual review and a player build remain unverified. The source Unity editor was left open without reloading its current scene. Preserve any unsaved editor scene edits before reopening the saved scenes. No Notion text was changed; no commit or push was made.

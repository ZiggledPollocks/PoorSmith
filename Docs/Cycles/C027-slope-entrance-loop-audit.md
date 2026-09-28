# C027 slope movement, cave entrance blend and loop audit — 2026-09-28

## Implemented

`PlayerMovement` now cancels gravity's tangential motion only while the authored player is idle on a marked, walkable `FieldOneWayPlatform` slope. Normal input, external wind, rolls, airborne travel and jumps do not enter this hold. The jump grace timer now distinguishes a real jump ascent from the positive Y velocity produced by running uphill, so grounded uphill movement can refresh jump eligibility. This does not change regular ground, unmarked terrain or the field's S-to-drop controller.

`CaveEntranceBackgroundTransition` now uses an 8-world-unit spatial crossfade between the existing camera-following forest and cave backgrounds in `FieldMapStructureTest`. It omits the hard forest SpriteMask in that scene and blends horizontally at the cave mouth and vertically toward the deep cave. Both backgrounds remain active during this spatial blend. `SampleScene` retains its existing timed transition. The authored scene, tile palette, Player sprites, animator and camera bounds were not modified.

## Current playable loop

The approved C025 probe found zero resource or monster instances in `FieldMapStructureTest` after their explicit removal; three assimilation statues remain. The C026 probe previously passed town→field→town travel, shared progress, equipped sword and material delivery, but it injected Wood/Stone into inventory. `BlacksmithSave.CreateDemo` supplies prototype crafting materials independently of field gathering. Therefore a travel and crafting prototype can be exercised, but the intended **gather → return → craft/equip → depart** loop cannot be completed by ordinary resource collection in the current playable field. The actual complete sequence was not observed in this cycle.

## Notion comparison (read only)

The [field revision](https://app.notion.com/p/3dada1bd22db803999ecdb1d5faaa535) specifies resource/monster respawns, a persistent one-time wind-crystal chest, hit-based gathering and distinct weapon behavior. The [exploration design](https://app.notion.com/p/3bfda1bd22db80d6a704e7beb7f2cecb) calls for an expanding map and warp return UI; the [wind theme](https://app.notion.com/p/1-3bfda1bd22db800cbce7ece4fadcb9bb) calls for special resources, enemies and a boss. The current playable `FieldMapStructureTest` has no scene resource/monster, treasure, warp or wind-region objects. `FieldSceneState` records visited cells, but `FieldHud` only displays day/gold/arrows/region, so the designed in-field discovery map is not accessible there. The [resource system](https://app.notion.com/p/3bcda1bd22db80fabc02e5d4a7a0a1a1) cannot be exercised without authored resources. Facility-shop products remain intentionally deferred under the user's earlier decision. The earlier user decision to treat assimilation as health intentionally differs from Notion's increasing-danger wording.

## Verification and limits

The modified runtime assembly built in the isolated project via `dotnet build Assembly-CSharp.csproj` with zero errors. `git diff --check` passed for the two installed scripts. An isolated Unity Play Mode probe was prepared to check idle slope drift, uphill-run jump and three entrance blend positions, but Unity Licensing Client repeatedly refused the batch editor connection; no probe result was produced. The only validation process was stopped without touching the live editor. C025/C026 are prior passed baselines, not post-change Play Mode results. Rendered blending, actual keyboard movement, full gameplay loop and player build remain unverified. No scene asset, Notion text, commit or push changed in this cycle.

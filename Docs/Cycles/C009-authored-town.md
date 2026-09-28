# C009 authored town routes and trade UI

## Authorized behavior

Use the user's newly saved `Assets/Scenes/SampleScene.unity`. ForestIn is in Town; ForestOut is in the gathering field. Preserve all existing transforms, art, terrain and unrelated settings. F/quick interaction opens the existing departure/return confirmation and fades out, moves the player and camera at full opacity, then fades in. Keep the approved daytime gathering restriction and return-to-storage transaction.

BlackSmith opens the existing additive smithy UI; pawnshop and Store open their Notion dialogue and transaction choices. Remove play-HUD smithy and save shortcuts. Keep the smithy exit, automatic saving, and save/load through the ESC settings screen and main menu. No commit/push is authorized by this implementation request.

## Sources and decisions

- Notion town: https://app.notion.com/p/3e1da1bd22db8052aa83fea3f9ed553e
- Equipment shop: https://app.notion.com/p/3e1da1bd22db80b0904fe57b4e0b305a
- Pawnshop: https://app.notion.com/p/3e1da1bd22db8094b8d0e9e501156d00
- User-supplied `Notion_Unity_UI_맵_구현_지침.md` is a design reference; Notion stays read-only.
- Store uses the established equipment shop (pickaxe, axe, bag, arrows). Existing provisional prices and tool-slot upgrades remain; this change does not invent itemized tool ownership or new balance.
- Fade durations reuse the existing cave portal timing, 0.15 seconds each way.

## Responsibilities

`TownSceneIntegration` stores references to the five authored objects. It binds existing IInteractable quick input at runtime, fits trigger bounds to each sprite without changing its transform, and selects the town/field camera bounds. The new saved campaign root shares existing fonts, item art, rules and row prefab assets.

`CampaignTransition` owns the fade overlay, input/pause lock, duplicate-transition rejection, and cleanup. `CampaignController` continues to own return transfers and departure rules. `SmithingLoop` remains the inventory and combined-save owner. Position snapshots now include sceneName so a NotionCampaign coordinate is not applied blindly to SampleScene.

`CampaignTradeUI` owns trade presentation; transactions still use CampaignEconomy and existing storage. No duplicate UI inventory or full-screen reference-image overlay is introduced.

## UI element plan

| Element | Static visuals | Dynamic data/input/result | Owner |
|---|---|---|---|
| Shop list and category filters | Reused row prefab and current tool/item icons | Pickaxe/axe/bag/arrows category, tier, provisional price, selected row | CampaignTradeUI |
| Shop information paper | Existing paper and paper-cap sprites | Selected description, owned state, purchase, insufficient-currency flash | CampaignTradeUI → CampaignEconomy |
| Pawn sale inventory | Existing bag and slot sprites; reused row prefab | 5×5 cells, counts, selected state, extra pages without discarding items | CampaignTradeUI |
| Pawn selection popup | Existing paper and item icon | Columns 1–3 show on left, columns 4–5 on right; quantity, unit/total price, sell | CampaignTradeUI → CampaignEconomy |
| Buyback list | Row prefab and separate information paper | Only previously sold stock; exhausted stock/selection disappear | CampaignTradeUI → CampaignEconomy |
| Gold, labels and counts | TMP font | Runtime text, no baked values | Authoritative campaign/storage state |

Graphics reuse previously extracted project sprites and existing tool icons. The reference images establish composition; their placeholder NPC portraits are not newly imported. No image is substituted for a whole working screen.

## Map element plan

| Element | Existing authored content | Integration / verification |
|---|---|---|
| ForestIn / ForestOut | Existing sprites and positions | Paired F interaction, confirmation/cancel, fade, input lock, camera cut, return transfer |
| BlackSmith / pawnshop / Store | Existing town buildings | Trigger bounds and shared IInteractable route; F tests with a weapon selected |
| Town/field | Existing SampleScene terrain and hierarchy | Preserve prior YAML objects, validate arrival and camera, no map regeneration |
| Save entry | Existing menu/settings architecture | No play HUD shortcut; ESC settings and automatic saving remain |

## Verification

Offline Unity-reference runtime and Editor compilation passed during preparation. Fresh isolated Play Mode run 49c05a6df906 passed 31/31 assertions with zero errors and unchanged source/user persistence. Existing scene objects: 278 unchanged; seven campaign-root blocks added and the SceneRoots list extended. Three 1920×1080 runtime captures were inspected. See Evidence/C009-validation.json. Compilation does not establish movement, collisions, UI appearance or persistent scene wiring.

## Remaining scope

This change does not claim complete Notion art, NPC portraits and all dialogue branches, full map expansion, missing alloy effects, or itemized tool purchases. Prior C008 limitations still apply. The source is inspected and tested in isolated copies; the user's live editor is not reloaded or saved by the agent.

Physical mouse raycast/keyboard use, a complete manual terrain traversal, all resolutions, a new Windows build and process-restart restoration were not tested for C009. ESC save access was exercised via the existing settings controller, not a physical Escape press. F interactions used Input System keyboard events. Existing C008 build evidence is historical.

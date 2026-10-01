# C055 Screen layout prefabs and isolated UI test scene

Retired 2026-10-01: `UITestScene` and its exclusive `UITestSceneController` were deleted at the user's request. The layout prefabs and production UI controllers remain. The scene-opening steps below describe the historical setup and must not be used as current validation instructions.

Date: 2026-10-01

## Scope

`Assets/JinHo/Resources/UI/Screens/Layouts` contains editable screen layout prefabs for the smithy, pawn shop, weapon shop, facility shop, smithy and field bags, map, equipment rack and storage chest. The prefab hierarchy uses the same names as the live UI. `ScreenLayoutTemplate` copies matching RectTransform placement to the live screen. It does not replace the existing UI controllers, button callbacks or data services. Smithy stage roots are excluded from placement copying so the station camera/pan can still move.

`Assets/Scenes/UITestScene.unity` is a standalone editor scene. It contains its own copy of the production blacksmith camera, Canvas, EventSystem and `BlacksmithController`, with save loading disabled. `UITestSceneController` opens the actual `BlacksmithController`, `CampaignUI`/`CampaignTradeUI`/`TownStorageUI`, `InventoryUIController`, and `FieldHud` screens. The campaign controller remains disabled but marked ready for real HUD refresh. The field state remains disabled but marked ready for the real field map. Town and field HUDs switch with the selected screen. Production UI methods, buttons and drag/drop handlers run against sample state.

`SmithingLoop` skips automatic game-session bootstrapping only for the exact `UITestScene` name. The test creates a disabled in-memory `SmithingLoop` with saving blocked. The production `CampaignController` is disabled, so its world update, travel and periodic save code never runs. The test scene is not added to the player build scene list. No other scene file was changed for this preview.

## Editing and review

1. Open a layout prefab in Unity Prefab Mode and move or resize named panels. Preserve the hierarchy names so placement still matches the live UI.
2. Open `UITestScene` alone and enter Play Mode. Use **UI 목록** to choose one of the nine real screens. The selector closes after a choice; press F10 to hide or show all test controls for an unobstructed Game View.
3. Use **아이템 지급** in the selector to add 99 of each catalogued item and discover active recipes in the temporary smithy and campaign chests. The editor God Mode overlay is hidden in this scene. The grant is once per test session.
4. Interact with the visible production controls. Gold, inventory, recipe discovery and chest contents disappear at the end of Play Mode. The smithy and campaign sample sessions are separate, so this scene is for screen behavior and layout review rather than cross-scene progression.

## Verification

The runtime and Editor assemblies compiled with zero errors after the HUD and grant changes. The Unity Editor was not reachable for a Play Mode interaction in this task; rendered Game View, pointer drag/drop, purchase, crafting and item grant behavior remain unverified.

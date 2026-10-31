# Current screen layout editing and isolated UI previews

Updated 2026-11-01. This replaces the historical C055 editing instructions. Current previews are `Assets/UITest/UITestScene.unity` and `Assets/UITest/EndingTestScene.unity`; the former `Assets/Scenes/UITestScene.unity` instructions remain obsolete. Neither preview is in the build list.

## Ownership and precedence

`GameArtSkin.asset` provides common PixelUI Wood defaults. `UIElementTemplate` on an authored element explicitly selects:

- **PrefabOverrides**: use the enabled visual properties from that element's Image, Selectable and TMP components. The supplied editing guides use this mode so per-screen edits persist.
- **WoodDefaults**: use the common skin for appearance. Placement/layout can still come from the guide.
- **PreserveRuntime**: do not copy appearance; protect the enabled appearance groups from generic skinning.

Screen template overrides take precedence over skin defaults and button ink correction. Unmarked legacy guides retain placement-only behavior. A matched guide does not instantiate content or copy listeners/services. Keep object names and hierarchy unchanged.

The live controller always owns text strings, icons, quantities, enabled/selected state, liquid values/materials, masks and gameplay. `imageColor` is disabled on selected/tinted slots and tabs; edit their Selectable colors instead. Do not enable image copying on data icons, or placement copying on scroll Content, map/graph contents, held tools, workpieces and animated gauge markers.

## Editing locations

All layout paths below are relative to `Assets/JinHo/Resources/UI/Screens/Layouts/`. Open the prefab in Prefab Mode, select the named object, and use its existing components. Guide-only placeholder text is never copied into the game.

| Screen | Prefab and object | Inspector editing |
| --- | --- | --- |
| Title | `../TitleMainMenu.prefab` → `Play`, `Continue`, `Settings`, `SaveList` | RectTransform, Image, Button states; child TMP label |
| Save list/confirmation | `../TitleSaveList.prefab` → existing eight rows and confirmation children | Same native prefab controls; preserve button references |
| Settings/menu | `../../GameUI.prefab` → `MenuCanvas` and existing settings hierarchy | Native panel/button/TMP components with UIElementTemplate |
| Town bag/chest | `TownInventory.prefab` → `TownBagPanel`, `TownChestPanel`, `TownItems/Viewport/Content` | Panel Image/RectTransform; Content GridLayoutGroup |
| Delivery | `DeliveryBox.prefab` → `TownItems/Viewport/Content`, `DeliveryItems/Viewport/Content` | Grid cell size, padding, spacing; item style below Content |
| Smithy chest | `SmithyChestOverlay.prefab` → `Bag/BagItems/Viewport/Content`, `ChestPanel/ChestItems/Viewport/Content` | Grid layout and `_ItemStyle` |
| Equipment rack | `SmithyEquipmentRackOverlay.prefab` → `Bag`, `ChestPanel/EquipmentItems/Viewport/Content` | Static layout/frame/typography; item icons remain live |
| Field bag | `FieldBag.prefab` → `Content/Backpack/ItemGrid` | GridLayoutGroup; `_ItemStyle` controls frames |
| Offering | `Offering.prefab` → existing `Content/Backpack/InventoryGrid`, `Content/StoneBasketArea/BasketSlots` | Grid/slot style; preserve liquid and offering data |
| Player info | `PlayerInfo.prefab` → `PlayerInfoModal/PlayerInfoPaper` | Panel, close button, title and account layout; account strings remain live |
| Field equipment/HUD | `FieldEquipment.prefab`, `FieldHUD.prefab`, `ToolHUD.prefab` | Existing named static frames/controls; tool icon movement remains controller-owned |
| World F prompt | `QuickInteractionPrompt.prefab` → `Background/KeyLabel` | Background and typography; target tracking/world scale stay in QuickInteractionPromptUI |
| Shops/pawn/facility | `WeaponShopBuy.prefab`, `WeaponShopSell.prefab`, `PawnShop.prefab`, `FacilityShop.prefab` | Named panels/controls and Content `_ItemStyle` |
| World map/save modal | `CampaignMap.prefab`, `CampaignSaves.prefab`, `CampaignModal.prefab`, `ReleaseDecision.prefab` | Surrounding controls; do not edit terrain/marker content through the guide |
| Smithy/station | `SmithyHome*`, `SmithyWorkshop*`, `SmithyStation_Workbench/Tools/Anvil/Quench/Furnace.prefab` | Named static control layout; original station illustrations/hit regions remain owned by the screen |
| Selection/fuel/working/result | `SmithySelection*`, `SmithyFuel*`, `SmithyCrafting_Workbench/Tools/Anvil/Quench/Furnace`, `SmithyResult*` | Existing panels, buttons and text; animated workpiece/gauge positions are excluded |
| Recipe map | `SmithyRecipesOverlay.prefab` → `RecipeBook/RecipeStation_*`, surrounding details panel | Toolbar/panel styles; preserve graph nodes, edges, category/parent tint and markers |
| Smithy HUD | `SmithyHUD.prefab` → existing HUD controls | Styling; Back position stays with its station-specific controller |
| Death | `DeathScreen.prefab` | Text/button layout; original dimming/fade behavior preserved |
| Dialogue/ending | `Assets/JinHo/Crafting/Resources/Story/StoryDialogue.prefab`, `StoryMiniDialogue.prefab`, `StorySkipConfirmation.prefab`, `StoryEndingScreen.prefab` | Native prefab UIElementTemplate/Image/Button/TMP; preserve portrait/content fields |

An `_ItemStyle` is a presentation prototype, not an extra inventory item. `listItemStyle` applies its enabled appearance and matching children to immediate runtime list entries; `itemNamePrefix` can restrict targets. Do not move/resize a layout-controlled item root: use the parent's LayoutGroup and the item's LayoutElement instead. Item content and events are not copied.

## Common adjustments

- Common sprites: `Assets/JinHo/Resources/UI/GameArtSkin.asset`. Changes affect unmarked/WoodDefaults controls. To follow a changed common skin, select WoodDefaults on the relevant guide/native element; to keep a custom image/color, select PrefabOverrides and enable the intended property groups.
- Position/size: RectTransform when `placement` is enabled. Anchors remain relative to the parent. The smithy stage root and moving content are deliberately excluded.
- Grids/lists: edit GridLayoutGroup cellSize/spacing/padding or Vertical/HorizontalLayoutGroup spacing/padding on Content. These settings apply when the corresponding screen/list instance is created; reopen the screen or restart Play after editing them.
- Text: edit TMP font size/min/max/autosize, color, margins and alignment, not placeholder text. The existing large-text preference still scales the authored limits. Runtime value colors have `textColor` disabled.
- Buttons: Image controls the base sprite/tint; Button controls transition, ColorBlock and SpriteState. Do not change events or targetGraphic references through a guide.
- Preserve the 1920x1080 ScaleWithScreenSize setup. Existing spacing uses a2-reference-pixel base (e.g.6/8 padding,8/12 spacing); do not round animated/scrolling positions every frame. Practical starting sizes: button height48+, slots64+, body text18–28 at reference resolution. Smaller controls require checking actual text/borders.
- Wood sprite border sums in source pixels: normal button8x9, inset8x8, window8x16. Actual UI border width depends on sprite/canvas pixels-per-unit and Image multiplier. Keep elements larger than their visible borders. Wood imports inspected: Point, no mipmaps, uncompressed. Integer RectTransform values do not guarantee pixel alignment at every resolution.

## Verification and limits

Independent Unity6000.3.11f1 compile and synthetic EventSystem Play checks passed. Real title/town/smithy/field UI path, three Game View sizes1920x1080/1280x720/1440x900, category/vertex focus, prepared real crafting-service UI, both ending previews and temporary prefab-edit roundtrip were exercised. Representative captures were inspected. Drag success/cancellation conserved items; reopening/list refresh retained custom settings; ordinary save/settings hashes were unchanged. Test-only authoring/driver scripts remain outside the production project.

These are not native OS input tests, a complete game-loop/minigame/story/combat regression, all-device visual coverage or a player build. See `../UIWood_Editing_2026-11-01.txt` and `../Evidence/UIWood_Editing_2026-11-01.json`. The UITest IMGUI selector is a test tool, not production UI.

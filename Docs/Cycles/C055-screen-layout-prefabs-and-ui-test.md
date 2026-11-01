## 기준 시안 기반 Wood 재구성 — 2026-11-01

기준: `Inventory-Wood-Reference-v3.png`. 실제 PixelUI Wood 스프라이트를 재사용하며 원본 그림과 레시피 그래프는 유지합니다.

- 공통 색·스프라이트·버튼 상태별 면 색/글자색: `Assets/JinHo/Resources/UI/GameArtSkin.asset`. `buttonInk`, `text`, 각 Face 색상과 `textFramePadding`/`textFramePixelsPerUnit`을 조절합니다.
- 화면 배치: `Assets/JinHo/Resources/UI/Screens/Layouts/*.prefab`. `UIElementTemplate.placement`와 RectTransform, `minimumHeight`를 편집합니다. 프리팹에서 정한 명시적 모양이 공통 스킨보다 우선합니다.
- 자동 버튼 글자색을 쓰려면 해당 텍스트의 `textColor`를 끕니다. 직접 정한 색을 유지하려면 켜고 TMP 색을 편집합니다. 가격·수량·아이콘·게이지·이벤트 등 실시간 데이터는 복사하지 않습니다.
- `_ItemStyle`은 목록 항목의 표시 규칙입니다. `LayoutGroup`과 `LayoutElement`로 행·셀·여백을 편집합니다. `itemNamePrefix`로 적용 대상을 제한합니다. 설정의 일반 행 규칙을 동적 접근성 영역 전체에 적용하지 않습니다.
- 설정창: 새 `SettingsMenu.prefab` 가이드가 타이틀과 인게임 설정에 공통 적용됩니다. TopTabs의 HorizontalLayoutGroup이 3개/4개 탭의 폭을 나눕니다. 저장/설정 처리 컴포넌트를 복제하는 프리팹이 아닙니다.
- 대장간은 기존 1600×900 내부 좌표계를 유지합니다. 마을·필드의 1920×1080 좌표를 그대로 넣지 않습니다. 기존 Canvas 배율 정책은 일괄 변경하지 않습니다.
- 가방/보관함 슬롯 수량 표시는 복원하지 않습니다. 수량은 기존 툴팁·선택 상세에 표시합니다. 긴 공통 툴팁은 스크롤하며 마지막 문장까지 읽을 수 있습니다.
- 미니 대화창 크기는 `StoryMiniDialogue.prefab`의 앵커 영역을 사용합니다. 목표를 피할 때 이 읽기 영역과 화면 안쪽 여백을 보존합니다.
- 새 표시용 Image/LayoutGroup이 가이드에 명시되면 기존 컨트롤에 추가될 수 있습니다. 새 장식 Image는 raycastTarget=false이며, 기존 입력 차단·버튼·이벤트를 대체하지 않습니다.

텍스트 검증은 TMP의 실제 글자 메시 범위, 실제 9-slice 테두리 안쪽, 겹침·잘림과 스크롤 접근성을 확인합니다. RectTransform 안에 있다는 사실만으로 통과하지 않습니다. 긴 이름·큰 숫자·큰 텍스트·세 지원 해상도를 포함하고, 실제 화면을 보지 못한 조건은 미검증으로 남깁니다. 최종 근거와 적용 파일은 `../UIWoodRedesign_2026-11-01.txt` 및 `../Evidence/UIWoodRedesign_2026-11-01/`를 참고합니다.

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

## Wood presentation refinements — 2026-11-01

Validated in the isolated `p` project and applied to batterMap. Common surface pixel scale and panel tint live in GameArtSkin.asset; explicit screen overrides remain authoritative. ToolSelectionHUD placement/size is edited on GameUI.prefab (430x200, scale.75, margin16, overscan0). Layouts/ToolHUD controls its frame; the controller retains icon selection/interpolation. Layouts/AssimilationHUD controls only the circular rim/placement; liquid, value and warning colors stay live. Recipe unfocused/category opacity lives in GameArtSkin (.78/.85).

[Screen/status matrix](../Evidence/UIWoodPolish_2026-11-01/ScreenMatrix.csv) and [report](../UIWoodPolish_2026-11-01.txt) separate rendered previews, prepared states, synthetic input, actual scene paths and unverified behavior. No rule/save/schema or scene/build-list change.

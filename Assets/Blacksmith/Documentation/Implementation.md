# 대장간 UI 구현

## 확인 범위

Notion은 읽기만 수행했습니다. 아래 문서 및 UI 문서의 실제 자식 문서만 확인했습니다.

- 시스템 기획서 / 장비 제작 시스템 / 대장간: `8769c4aa4b9c83d9ba8d817b0ead5278`
- UI 기획서 / 장비 제작 시스템 UI 전체 정리: `0099c4aa4b9c833fbf8f812e5c6a2d9f`
- 대장간 탭 오브젝트 배치: `8859c4aa4b9c836792cd817612d36376`
- 크래프팅 화면 UI: `9069c4aa4b9c83f9b2ba017d40db9239`
- 용광로 / 도구 걸이 / 모루 / 담금질 물 / 작업대 / 상자 / 간이 침대 UI
- 추가 요청: 데이터 베이스 / 크래프팅 레시피: `8879c4aa4b9c831884ad815a79adf309`

추가 요청한 크래프팅 레시피를 확인했습니다. 그 외 전체 아이템 & 장비, 숙련도 레벨표, 합금 효과, 날짜/시간 문서는 열지 않았습니다. 본문 일부 링크는 이전 페이지 ID를 가리키므로 UI 기획서 아래 실제 자식 페이지로 이동했습니다. 대장간 본문에는 직접 자식 페이지 링크가 없었습니다.

## 기존 코드 확인

대상은 Unity 6000.3.11f1 URP 2D 기본 프로젝트입니다. Assets에 기존 Inventory, Item, Player, Interaction, Tool, GameManager, UIManager, SaveSystem 스크립트가 없어 신규 도메인 계층을 작성했습니다. 기존 SampleScene과 InputSystem_Actions는 보존합니다.

## UI 요소 분석

| 요소 | 역할 / 정적·동적 | 데이터 | 입력과 결과 | 시스템 / 스크립트 |
|---|---|---|---|---|
| 설비 오브젝트 | 독립 정적 Sprite + 동적 Button | 현재 설비 | 클릭 → 전용 화면, hover 색 강조 | BlacksmithController / BlacksmithView |
| 재료 슬롯 | 정적 프레임 + 동적 아이콘·수량 | Stack | 좌클릭 1개 선택/반환 | InventoryService / InventorySlotView |
| 보관함·배낭 | 동적 목록 | 순서가 있는 Stack 목록 | 검색, 분류, 휠, 스크롤바, 드래그 정렬 | InventoryService |
| 자동 제작 목록 | 정적 양피지 + 동적 Prefab | RecipeDefinition, RecipeProgress | 수량 조절 / 재료 자동 배치 / 제작 | CraftingService / RecipeEntryView |
| 연료 | 동적 수치 | SaveData.fuel | 재료 1개 소모, 3/6/1 증가, 최대 50 | InventoryService.AddFuel |
| 도구 조작 | 정적 도구 Sprite + 동적 입력 | 도구 종류, 손질 횟수 | 위 드래그 / 왕복 / 클릭 | CraftGestureInput / CraftingService |
| 모루 5지점 | 동적 Button 5개 | 방향별 횟수 | 총 5회 후 결과 판정 | CraftingService.Hit |
| 담금질 게이지 | 동적 Image 위치 | 시간, 무작위 성공 구간 | 유지 후 해제 | CraftGestureInput / BlacksmithController |
| 조립 원 | 동적 Button / 축소 Image | 대상 수, 총점 | 4/2/0점 판정, 실루엣 공개 | CraftingService.Timing |
| 결과 | 동적 아이콘·이름·수량·품질 | CraftResult | 확인 → 설비로 복귀 | BlacksmithController |
| 레시피·도감 | 동적 제작 경로 목록 | 발견한 레시피, 숙련도 | Tab / 책 / 두루마리 열기와 복귀 | RecipeProgress |
| 장비 칸 | 동적 장착 상태 | EquipmentEntry | 클릭, drag 장착, 클릭 해제 | InventoryService.Equip |
| 침대 | 확인 팝업 | 일수, 오전/밤, HP | 취침 → 시간 전환, 체력 회복 | SaveData / BlacksmithController |

## 이미지 및 계층

원본 화면을 통째로 사용하는 Image와 투명 버튼 오버레이는 없습니다. `Art`의 PNG 35개를 각각 Image/Button에 할당합니다. 원본 좌표와 최소 재구성 내역은 `AssetExtraction.json`에 기록했습니다. 텍스트·수량·품질·연료·장비 수치는 모두 TextMeshProUGUI로 표시합니다.

```text
BlacksmithGame
  BlacksmithController → InventoryService / CraftingService / SaveData
BlacksmithUI (Canvas, CanvasScaler, GraphicRaycaster)
  StationStage (벽/테이블/설비/도구 각각 독립 Image)
  InteractivePanels
    InventoryPanel / GridLayoutGroup / InventorySlot prefab instances
    SelectedMaterials / GridLayoutGroup / InventorySlot prefab instances
    AutomaticRecipes / VerticalLayoutGroup / RecipeEntry prefab instances
    Minigame / ResultPanel / RecipeBook / ChestPanel
  HUD / TextMeshProUGUI / Tooltip
EventSystem (InputSystemUIInputModule)
```

상태: Home → Station → Selecting → Playing/Animating → Result → Station. Fuel, Chest, Recipes, Codex, Sleep은 별도 상태입니다. Selecting 중 닫기/초기화는 재료를 반환합니다. Playing/Animating/Result 중 설비 이동을 막습니다. 레시피는 애니메이션·결과 이외 상태에서 열 수 있고 원래 상태로 돌아옵니다.

## 기획 충돌 및 테스트 데이터

- 담금질 성공: 시스템 본문은 초록, 상세 UI는 노랑 또는 초록입니다. 상세 UI 기준을 적용했습니다.
- 모루는 레시피 문서의 총 5회 패턴을 적용했습니다. 철판 중앙5, 철괴 각1, 긴 칼날 상2/하2/중앙1, 짧은 칼날 상1/하1/중앙3입니다. 이전 임시 패턴은 교체했습니다.
- 레시피는 추가 허용된 문서를 적용했습니다. 활성129/보류60 경로입니다. 수치표는 여전히 범위 밖이며 장비 수치/전용 아이콘은 미정, 5회 제작 시 숙련도2 도달은 테스트 기본값입니다. 자세한 내용은 `NotionRecipes.md`에 정리했습니다.
- 장인급은 품질 타입·가격 배수만 지원합니다. 해금 조건표를 확인하지 않았으므로 임의로 획득시키지 않습니다.
- 합금 효과는 장비 정의의 specialEffect를 표시할 수 있지만 임의 효과를 넣지 않았습니다.
- 외부 채집/마을 씬은 존재하지 않습니다. 배낭은 공유 SaveData에 저장되어 추후 동일 InventoryService로 연결할 수 있습니다.
- 침대는 아침→밤, 밤→다음날 아침으로 처리합니다. 마을 전용 애니메이션과 D-day 목표값은 자료가 없어 일수 전환 화면으로 대체했습니다.
- 참고 UI의 회색 재료 모형, 분리 불가능한 영역과 원본에 없는 일부 아이콘은 단순한 개별 Sprite로 재구성했습니다. 원본 배치 전체를 그대로 재현한 최종 아트 버전은 아닙니다.

## 플레이

`Assets/Blacksmith/Scenes/BlacksmithShop.unity`를 열고 Play 합니다. `Blacksmith/Build playable scene and verify` 메뉴는 씬·프리팹 재생성 및 도메인 검증을 실행합니다. 해당 메뉴는 현재 씬을 새 씬으로 바꾸므로 저장할 작업이 있으면 먼저 저장하세요.

- 원석을 용광로에 넣기 → 시작 → 달궈진 철
- 모루: 달궈진 철에 상2/하2/중앙1 → 달궈진 긴 철 칼날
- 담금질: 재료를 누른 채 게이지가 노랑/초록일 때 놓기 → 긴 철 칼날
- 나무 원목 → 톱5~6회 → 나무 판자 → 톱3~4회 → 나무 토막 → 칼6~7회 → 나무 자루
- 이끼 슬라임의 점액2 → 용광로 → 강력한 접착제
- 작업대: 긴 철 칼날1 + 나무 자루1 + 강력한 접착제1 → 철 검
- 홈의 테스트 재료 받기: 저장 초기화 없이 새 채집 재료를 보충합니다.
- 상자: 우클릭으로 배낭↔보관함 전체 스택 이동. 좌클릭 하나 / 길게 클릭 전체 들기. 반대편 슬롯 또는 +칸 클릭 배치. 드래그 순서 변경.
- 장비 탭에서 보관함 장비 클릭 또는 해당 칸에 드래그. 활·망치는 방패를 해제/잠금, 활만 화살 허용.
- 저장: Application.persistentDataPath 아래 `blacksmith-ui-save-v1.json`. 테스트 실행은 저장을 비활성화합니다.

## 검증

Editor builder의 도메인 검증은 재료 보존, 종류 제한, 연료 상한/소모/부족, 도구 재질 제한, 부산물, 모루 횟수/패턴, 담금질 실패, 품질 경계, 조립 점수, 양손 무기/화살 제한, 자동 재료 부족 시 원자성을 검사합니다. 별도 player smoke harness는 `--blacksmith-smoke`를 명시할 때만 실행되어 화면 캡처를 저장합니다.

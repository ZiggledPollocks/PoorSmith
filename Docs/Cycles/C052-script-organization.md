# C052 — 자체 스크립트 책임 정리와 함수 지도

## 범위

- 자체 C# 185개: `Assets/JinHo` 126개, `Assets/Campaign` 46개, `Assets/SettingsMenuUI` 13개.
- Campaign 루트에 흩어져 있던 런타임 스크립트 43개를 `Core`, `Combat`, `Travel`, `Field`, `Town`, `World`, `UI`로 이동했다. 기존 `Editor` 스크립트 3개는 그대로 두었다.
- 나머지 두 루트의 이미 구분된 폴더와 클래스 이름은 유지했다. 씬·프리팹의 직렬화 참조가 따라가는 스크립트 `.meta` GUID는 변경하지 않았다.

## 의존 관계를 읽는 순서

1. 화면 입력과 표시: `Campaign/UI`, `JinHo/**/UI`, `SettingsMenuUI`.
2. 게임 흐름: `Campaign/Core`, `Campaign/Travel`, `JinHo/Crafting/Integration`, 필드·전투 Controller.
3. 규칙과 상태: `Crafting/Scripts/Domain`, `JinHo/Shared`, `CampaignState`.
4. 저장: `JinHo/Scripts/Persistence`, `BlacksmithSave`, `SmithingLoop`의 통합 저장.

이 순서는 버그 조사 경로다. 실제 호출 방향은 Obsidian 각 문서의 **프로젝트 내부 호출**과 **호출자**를 기준으로 확인한다. Unity 메시지, Inspector 참조, 이벤트는 정적 호출 목록에 모두 나타나지 않을 수 있다.

## 주석과 함수 문서

- 모든 185개 스크립트 머리에 파일 책임·주요 함수·Obsidian 문서 경로를 적었다.
- 20줄 이상인 함수 중 기존 설명 주석이 없는 함수에는 첫 분기·상태 변경·프로젝트 내부 호출을 짧게 적었다. 조건이 없는 함수나 기존 주석이 있는 함수에는 억지 설명을 추가하지 않았다.
- `batterground/코드해체분석기/00_프로젝트_스크립트_지도.md`에서 폴더별 파일과 증상별 시작점을 찾는다. 스크립트별 문서는 실제 `Assets` 경로를 그대로 따르며 2,262개 함수·생성자·지역 함수·속성 접근자를 다룬다.
- 각 항목에 선언과 입력, 반환, 처리 순서, 분기, 상태 변경, 호출 대상, 호출자, 버그 추적 시작점을 표시한다. 줄 번호와 해시는 문서 생성 시점 소스 기준이다.

## 검증과 한계

- 이동한 스크립트 43개와 `.meta` 43개는 이동 전후 SHA-256이 같다.
- 주석을 제거하면 185개 파일이 주석 작성 전 백업과 바이트 단위로 같다. 별도 코드 수정은 `CampaignBuildScene.Route`의 `Enumerable.Reverse(points)` 컴파일 오류 수정 1건이다.
- Roslyn: 소스 317개 파싱, 대상 185개 분석, 구문 오류 0건, 의미 오류 0건.
- 문서 186개의 존재, 185개 원본 해시, 2,262개 항목, 모든 내부 Markdown 링크를 검사했다. 기존 문서 119개는 교체 전 별도 백업했다.
- Unity Editor 리로드, Play Mode, 플레이어 빌드는 실행하지 않았다. Unity가 `.meta`를 재가져온 뒤 Console 오류와 대표 씬의 컴포넌트 연결을 확인해야 한다.

# Functional QA checklist — partial verification

각 행의 전체 범위가 끝나지 않으면 상태를 미실행으로 유지하고, 실제 통과한 부분은 마지막 열에 별도로 기록한다. 후속 전체 QA는 선행 스토리 검증과 승인된 GitHub 백업 이후 시작한다. 한 화면을 열었거나 코드만 읽었다는 이유로 통과 처리하지 않는다. 정상 진행과 격리 상태 준비 시험을 구분한다.

저장된 씬: Assets/JinHo/Scenes 아래 TitleScene, SampleScene, FieldMapStructureTest, BlacksmithShop, NotionCampaign. 최신 일반/Development 빌드는 기존 활성5개 씬 연결을 유지했다.

| ID | 기능·진입 경로 | 사전 상태 | 조작 | 기대 결과·확인 데이터 | 상태 | 문제·재검증 |
|---|---|---|---|---|---|---|
| T01 | 타이틀/TitleSceneController.Play | 저장 없음/있음 | 새 게임 확인·취소·반복 | 취소 불변; 확인 초기 진행/설정 보존 | 미실행 | 부분 검증: 저장 없음 새 시작/있음 취소 통과. f310 초기화 확인/3565 잠금 실패 UI·기존 저장 보존 통과. 전체 반복/설정 조합 남음. |
| T02 | 이어하기/TitleSceneController | 없음/유효/손상 | 이어하기 선택 | 최신 유효 저장과 일치; 마을 시작 | 미실행 | 부분 검증: 7bee/0a8826 실제 이어하기 및 마을 시작 통과; 3565 손상 선택/최신 유효 UI 통과. 모든 손상 유형 남음. |
| T03 | 저장 목록/TitleSaveList | 1개/8개 | 각 항목 예·아니요 | 시간·순서·지정 파일 정확; 취소 불변 | 미실행 | 부분 검증: 실제 1개 목록 선택·취소 통과. f310 8행 취소·3번째 실제 로드 통과. 모든8행 실제 로드 남음. |
| S01 | 자동 저장/SmithingLoop.SaveProgress | 같은 날/9번째 날/과거 로드 | 반복 저장 | 현재 프로젝트 저장순서별 최대8; 성공 뒤 오래된 항목 정리; 과거 날짜 재저장 최신 | 미실행 | 7bee 격리 서비스: 8→9, 과거 날짜 재저장 순서 통과. 실제 아침 연속 자동 저장 남음. |
| S02 | 실패/저장 서비스 | 격리 쓰기 실패 | 초기화/수동/자동 저장 | 기존 파일 보존; 오류 안내; 초기화 실패 시 시작 금지 | 미실행 | 7bee 파일 잠금 실패·초기화 롤백 통과. 3565 초기화 실패 화면 안내, 0f93 엔딩 실패 재시도 통과. 모든 실패 지점 남음. |
| S03 | 로드/LoadSlot,LoadHistoryFile | 장비·재료·날짜·목표 | 저장→Play 종료→재진입·로드 | 금액/아이템/도구/진행/목표 일치 | 미실행 | 7bee 새 Unity 프로세스 불러오기 장비·상자·날짜·금액 통과. 목표·수동 슬롯 남음. |
| D01 | 대화/StoryTutorialView | 새 게임 | Space/S 유지·해제 | 완성→다음; 로컬2배; 게임 입력 차단 | 미실행 | 실제 InputSystem Space/S 1bc1/e3f 통과. 네이티브 자동화 키 전달은 환경상 확인 불가. |
| D02 | 스킵/FinishSkip | 인트로/제작/결과창 | 취소·확인·반복 | 정확 복귀/최소 상태; 보상 중복 없음 | 미실행 | c1cf 실제 활성 톱 미니게임 스킵 취소/확인·재료 반환·출력 미지급·최소 장비1회·저장 통과. 5d34 결과/7dd 필드 tree 스킵 통과. 모든 단계 조합 남음. |
| D03 | 일반/미니/강조 | 긴 본문/여러 재료 | 세 해상도·선택·드래그 | 잘림·겹침 없음; 선택한 강조 제거; 실제 입력 제한 | 미실행 | 8625948b5119 긴 일반 4페이지/미니 8페이지 내용 보존·버튼·배타 표시 통과; 세 해상도 합성 캡처 확인. 실제 대상 강조/모든 해상도 네이티브 확인 남음. |
| D04 | 제작 튜토리얼/RunTutorial | 원목5 | 톱→레시피→칼→숫돌→작업대 | 실제 완료로 진행; 잔여 재료/출력 일치 | 미실행 | 1bc1 실제 톱→레시피→칼→숫돌→작업대→검 통과. |
| D05 | 실패 보완/GrantOnce | 원목·판자0/잔여 있음 | 실패·재진입 | 조건 만족 때1회만 보완; 연료 유지 | 미실행 | 30ed7aee9aa5 재료0 준비→보완1·반복 불변·자루보존·저장 통과. 모든 실패 조건 조합 남음. |
| D06 | 물품 회수/RecoverCarriedMaterials | 가방·장비·보관함 | 문F→박덕수 대화 | 소지 일반 재료만; 필수품/검/보관함 보존 | 미실행 | 32db 실제 소지 원목/생가죽 회수·핵/칼날/자루·상자 보존/재회수 불변 통과; 모든 보호 물품 조합 남음. |
| D07 | 장착 튜토리얼/InventoryService.Equip | 검·낡은 도끼 | 장착·다른 행동 | 모두 장착 시 진행; 잘못된 행동 차단 | 미실행 | 1bc1 제작 검·도끼 실제 거치대 장착 통과; 무관한 행동 전체 남음. |
| D08 | 채집 튜토리얼/ResourceHarvestWorkflow | 숲 진입 | 나무→원목15/가죽2→석상→귀환 | 실제 수량/동화율/구역 제한 일치 | 미실행 | 1bc1 실제 나무3·사슴 드롭·자동 획득·원목15/생가죽2 목표 통과. 근접 좌표 준비, 정상 도보 미검증. |
| D09 | 납품 튜토리얼/ShowStock | 가지0/홀수/짝수 | 지정 수량·닫기 | 최소1/절반 내림; 지급1회; 목표 고정 | 미실행 | 1bc1 실제 가지 납품 통과; 32db 가지0 보완1·실제 납품·수면 통과. 홀수/짝수 전체 경계 남음. |
| D10 | 수면 튜토리얼/SmithyStation | 귀환 저녁 | 상자·침대 | 날짜 증가; 완료/입력 복구 | 미실행 | 1bc1 실제 수면→다음 아침→튜토리얼 완료/저장 통과. |
| D11 | 엔딩/TryEnding | 격리8일 아침; >=1500/<1500 | 마을 복귀·재진입 | 분기1회; 실제 차감/0G; 타이틀 복귀 | 미실행 | 667f 실제 튜토리얼 Day2 저장→수면12회→Day8 실패 엔딩 통과(정상 채집 7일 아님). e4bed 저장 실패 재현→0f932 재시도/1회 차감/저장 후 타이틀 통과. |
| M01 | 이동/PlayerMovement | 평지·경사·발판·벽 | 좌우/달리기/점프/S/롤/넉백 | 접지/통과 규칙 유지; 끼임 없음 | 미실행 | 4f774 실제 타운 걷기/달리기/Shift 해제/점프 착지/구르기 통과. 2823e 실제 중간 발판 착지/S/복구·낮은 모서리 Space 통과/착지 통과. 전체 경사·인접 경로·옆면·넉백 남음. |
| M02 | 이동/FieldSceneTravel | 마을·숲·동굴·대장간 | F→예/아니요·반복·왕복 | 좌표/카메라/페이드; 가방/장비/동화율 유지 | 미실행 | 05f 실제 동굴 진입/출구 핸들러·페이드 왕복/반복 차단·숲/동굴 좌표·장비/금액/저장 통과. 정상 도보/F/시각 전체 남음. |
| M03 | 상호작용/PlayerInteraction | 자원·문·석상·상점 | quick/F·UI 중 입력 | 도구 전용 quick; F 동작; 닫은 뒤 입력 복구 | 미실행 | 후속 QA 미착수 |
| C01 | 도구/ToolSelectionHUD,ApplyEquipment | 무기·등급별 도구 | 선택/장착/해제/로드/이동 | 각 아이콘/티어/리치/강조/전투 수치 일치 | 미실행 | e147 실제 무기별/도끼·곡괭이 등급별 장착·선택 HUD 아이콘 통과. 모든 해제/재로드 조합 남음. |
| C02 | 채집/ResourceHarvestWorkflow | 맞는·틀린·낮은 도구 | 타격/파괴/획득 | 자원/드롭/기록; 잘못된 도구 무손실 | 미실행 | 1bc1 실제 도끼 나무·검 사슴·드롭 획득 통과. 틀린/저티어/광물 남음. |
| C03 | 망치/CampaignCombat | 같은 적·검/망치 | 동일 시간 공격; 중간 교체 | 공격 간격 제한; 망치 넉백; 시작 무기 효과 유지 | 미실행 | e147 동일 조건 물리프레임 검13/망치10 공격·빠른 반복 차단·공격 시작 망치의 넉백1.25 유지 통과. 자연 명중 거리/애니메이션 전체 남음. |
| C04 | 방패/ReceiveDamage | 장착/없음; 좌/우 | 전후방 근접·투사체; 유지/해제 | 일반 방어구 뒤 전방만 방패 효과; 미상 방향 제외 | 미실행 | 557e 좌우 합성 이동/우클릭, 전후방·투사체 방향·미상·해제·미장착 방어 피해 통과. 실제 몬스터 공격/특수 방패 효과 남음. |
| C05 | 활/투사체 | 화살 있음/없음 | 당김·해제·지형/적 명중·회수 | 속도/중력/조준; 소비/회수 중복 없음 | 미실행 | 7c8075 실제 화살 중력·지형/준비 적 충돌·1회피해·드롭·탄약 소비/회수 데이터 통과. c85d6b 실제 합성 당김/해제·커서 카메라·탄약1회·UI 취소 통과. 자동줍기·네이티브 입력 남음. |
| C06 | 몬스터/각 Controller | 숲·동굴 | 탐지/공격/벽/사망 | 영역 제한; 피해/넉백/드롭; 골렘1체 | 미실행 | e08c 박쥐 양 프리팹 합성 장애물, a8c 실제 접촉/넉백/사망/드롭, 8a6 슬라임/숲동굴 영역 복구 통과. 자연 AI 전체 경로/시각 남음. |
| C07 | 골렘/StoneGolemStates | 곡괭이/일반; 근·원거리 | 기상/걸음/공격/멀리 이동 | 피해 구분; 비행 무피해; 착지1회; 체력 유지 복귀 | 미실행 | a22c/1fabe 실제 골렘 대기·곡괭이13/무기무효·기상·5걸음·근접25/돌착지20·복귀 체력/드롭 통과. 자연 AI 모든 지형·시각 남음. |
| I01 | 가방·상자/InventoryService | 빈/가득/무게/품질 | 수량 이동/클릭/닫기 | 상한·품질·수량·기록 일치; 총량 보존 | 미실행 | ae421 85kg 실제 필드 무게 상한/초과/잘못된 수량 보존 통과. c56/b1d 55/85kg 씬 동기화 통과. 모든 품질/이동 조합 남음. |
| I02 | 드래그/InventorySlotView,ItemDropTarget | 유효/무효/가득 | 드래그/취소/닫기/반복 | 성공1회; 실패 복귀; 중복·유실 없음 | 미실행 | a10060 실제 마을 보관함 양방향 드래그·무효취소·재열기·저장·가방변환 통과;11f65 거래 드래그 통과. 모든 화면/닫기중 드래그 남음. |
| I03 | 레시피/RecipeBookView | 미발견/발견/숙련 | 노드·엣지·배치 | 발견 노드/모든 재료 연결; 목표·배수 정확 | 미실행 | 928440 활성 레시피 전체 재료 엣지·발견 노드·UI 갱신·검 재료 배치·닫기/저장 통과. 준비된 발견 상태, 네이티브 그래프 드래그/목표 재로드 남음. |
| I04 | 제작/CraftingService | 각 작업대·부족·배수 | 순서 변경/성공/실패/취소 | 조건 안내; 출력/연료/잔여물·실패 규칙 유지 | 미실행 | e76c 용광로/모루/담금질 자동2배수; a1ab 모루·담금질 수동 성공/실패/환불; a9ef 작업대 타이밍/결과/저장 통과. 모든 순서/잔여물 조합 남음. |
| I05 | 거치대/DrawEquipment,DrawRackInventory | 한손/양손/활/방어구 | 장착·해제·교체 | 슬롯 제한/출처; 실제 통계/HUD 갱신 | 미실행 | 023f/5be 실제 출처 보존 장착·해제·한손 검/망치·활/양손 망치 슬롯·저장 재열기 통과. 모든 방어구/특수효과 표시 남음. |
| E01 | 무기 상점/CampaignTradeUI | 돈·공간 부족/빈 | 구매/판매/수량/드래그/취소 | 현재 분류 유지; 금액·재고1회; 표시 갱신 | 미실행 | 11f65 실제 도구 구매/전체 판매/드래그 취소/반복 클릭 통과. 아이템 구매·전체 분류/공간 부족 남음. |
| E02 | 시설 상점/ShowDialogue | 마을 | F·대화 종료 | 현재 준비 중/상품 동작 유지 | 미실행 | 후속 QA 미착수 |
| E03 | 전당포/ShowPawn,SellToPawn,PawnBuy | 물품·부족·과거 대출 | 판매/재구매/취소/반복 | 대출 제거; 담보 이동; 금화·물품 일치 | 미실행 | 11f65 실제 물품 판매/보관품 반환/반복 클릭/금화 부족 안내 통과. 모든 빈 목록·품질·과거 담보 UI 남음. |
| E04 | 납품/Deposit,Withdraw,Morning | 물품/빈/정산 후 | 넣기/빼기/수면/수금 | 선택 유지; 정산/금화1회; 취소 총량 보존 | 미실행 | 후속 QA 미착수 |
| E05 | 수면·빚/Morning | 주간/스토리/미정산 | 아침·대장간 출구 | 날짜/납품/빚 분리; 중복 정산 없음 | 미실행 | 후속 QA 미착수 |
| U01 | 동화율/OfferingUI | 마을·필드/사용한 석상 | 피격/제물 클릭·유지/ESC | 동기화/증가·감소; 입력·속도 복원 | 미실행 | 6b4f 실제 유지 추가·중복 클릭 억제·합성 Esc 취소 재료 전량/바구니 초기화·속도1·再열기 확정 통과. 일반 시간 증가/구역 동기화 전체 남음. |
| U02 | 사망/복귀 | 가방 재료·장비 | 죽음→마을/타이틀→이어하기 | 가방 손실; 장비·진행 유지; 지면에 플레이어 표시 | 미실행 | 6ae417 실제 필드 사망·가방손실·4%금화감소·마을버튼·지면/장비/상자/입력 보존 통과. 타이틀경유 추가 남음. |
| U03 | 메뉴/GameUIController | 마을·필드·작업실 | ESC/E·설정·저장 종료·복귀 | 중복 입력 없음; 일시정지/입력 복원; 설정 보존 | 미실행 | 9b85 설정4탭/실제 슬라이더·격리 PlayerPrefs 저장 재열기·메뉴닫기 입력1 통과. 네이티브키/모든 화면 저장종료 남음. |
| U04 | 지도·장비·정보 HUD | 각 구역/날짜·금액 변경 | 열기/닫기/ESC/교체 | 실제 위치/무게/장비/금액; 클릭 가능 | 미실행 | 52da 워프 집버튼 중복 수정·왕복목록 취소/1개 유지/필드워프/일반지도 잔류 없음 통과. 전체 날짜/금액 실시간 표시 남음. |
| U05 | Editor God Mode | 준비 전/후/지급 후 | 2회/이동/저장/종료 | 99·발견1; 일반 저장 해시 불변; 빌드에 없음 | 미실행 | 9c9508 지급·발견·중복방지·왕복·자동수동저장·종료후 일반저장 보존 통과; 실제 Game View 버튼 클릭 확인. 5a81 Normal/Development 빌드 성공·God Mode 타입 제외 통과. 빌드 실행 화면·도메인재로드·잘못된자료 남음. |


## Executed atomic cases — 2026-10-10

상위 표는 아직 끝나지 않은 전체 범위 목록입니다. 아래는 실제로 실행한 세부 검사입니다. 준비된 상태·합성 입력·직접 핸들러·렌더 캡처·네이티브 클릭을 구분합니다. 통과는 해당 행의 범위만 뜻하며 모든 경계 조합의 통과를 뜻하지 않습니다.

| ID | 기능/진입 | 사전 상태 | 조작 | 기대 결과/데이터 | 상태 | 근거/남은 범위 |
|---|---|---|---|---|---|---|
| T01a | 타이틀 새 게임 | 독립 기존 저장8 | Play→취소/확인 | 취소 바이트 불변; 확인 Day1/새 이야기/이전 진행 제거 | 통과 | f3107128e72d; 합성 버튼 |
| T02a | 손상 저장/이어하기 | 최신 손상·이전 정상·파일 잠금 | 목록 선택 후 손상/초기화/Continue | 시작 거부·오류·원본 보존·Day7 최신 유효 로드 | 통과 | 356508b9111b; 준비 파일 |
| T03a | 저장 목록 | 날짜별8개 | 8행 확인 취소·3번째 예 | 8→1 순서/시간/취소 불변/Day6 정확 로드 | 통과 | f3107128e72d; 전체8개 실제 로드는 미실행 |
| S01a | 이력 파일 서비스 | 0/8개 및 과거일 저장 | 8→9 저장·과거일 재저장·잠금 실패 | 성공 뒤8개/저장순서/실패 보존 | 통과 | 7bee02a05dde; 서비스 경계 |
| S03a | 프로세스 재진입 | 이전 격리 플레이 저장 | Unity 새 프로세스→Continue | 장비·품질·날짜·재료·소수G 보존 | 통과 | 7bee02a05dde |
| S03b | 미완료 저장 재진입 | 검 chest/도끼 bag·blade 단계 | 실제 Continue 두 번 | 이야기 반복 없음/품질·재료·날짜/중복 지급 없음 | 통과 | 5e81eab55c95; 준비 상태 |
| D01a | 이야기 입력 | 신규 게임 | InputSystem Space/S 유지·해제/무관 입력 | 완성→다음/로컬2배/차단/속도 복귀 | 통과 | 1bc1e7f678db; 합성 키 |
| D02a | 스킵 취소/확인 | 인트로/진행중 톱/완료 결과/필드 tree | Skip Cancel→Confirm | 재료 반환 또는 완료 출력 유지/최소 장비 중복 없음/입력복구 | 통과 | 48fc11a3de57,c1cf4526344b,5d34723c0fb4,7dddfb30e9b3 |
| D03a | 긴 일반/미니 대화 | 긴 테스트 문장 | Space/Next 3해상도 렌더 | 문자 보존·페이지·배타 표시·겹침 확인 | 통과 | 8625948b5119; 합성 화면, 실제 모든 대상은 미실행 |
| D03b | 실제 튜토리얼 강조 | 마을 실제 버튼2/대상 | 다중 강조→하나 제거→mini | 실제 좌표/부모·배치 불변/클램프·입력복구 | 통과 | 6014f6e14806; 좌표 준비 |
| D04a | 제작→채집→수면 튜토리얼 | 새 게임 | 실제 UI 톱/칼/숫돌/조립/장착/나무·사슴/제물/귀환/납품/침대 | 검 완성/wood15 raw_leather2/목표 진행/Day2 완료 | 통과 | 1bc1e7f678db; 채집 위치·피해 준비, 정상 도보 전체 아님 |
| D11a | 엔딩 | Day8/1700.5 또는400.5 및 저장 잠금 | 대화→엔딩/저장 실패→재시도 | 1500 한 번/잔금200.5 또는0/기존 파일 보존/저장 후 Title | 통과 | 0c72772a3eed,279977736e13,0f932961623a; 조건 준비 |
| D11b | 수면 날짜 경로 | 튜토리얼 완료 Day2 | 실제 침대12번 | Day8 아침 엔딩 한 번 | 통과 | 667fcbc3fea2; 7일 정상 활동 아님 |
| M01a | 마을 이동 | 실제 Player 평지 | 합성 방향/Shift 해제/Space/roll | 걷기5.5 달리기8.25/해제 감속/점프·착지 | 통과 | 4f7745557d47; focus 설정 복사본만 |
| M01b | 한 방향 발판 | 실제 middle/corner·위치 준비 | 점프/착지/S/복구 | 윗면 접지/하강/충돌 복구 | 통과 | 2823e1568cc2; 전체 경사·연속 경로 미실행 |
| M02a | 씬/숲·동굴 이동 | 마을/실제 passage 위치 | TravelYes/entry exit/반복 | 목적 좌표·숲/동굴·상태·페이드·입력 | 통과 | 05f166277637; 직접 핸들러, 네이티브 F 아님 |
| C01a | 도구 HUD | 실제 카탈로그 무기/axe/pick | 장착→선택 | 아이콘·현재 ID·선택 위치 | 통과 | e1472e918243 |
| C02a | 다섯 자원 | 실제 Spawn prefab·격리 도구 | 도구 종류 gate/Interact 소진·드롭 | 틀린 종류 거부/올바른 횟수 드롭·소진 | 통과 | 54e21f3d0ca6; 직접 Interact, 마우스 타격 미실행 |
| C02b | 저티어 자원 | 철 인스턴스 tier3/곡괭이1 | 18회 직접 타격 | 계수0·자원 미소진 | 통과 | 54e21f3d0ca6; 명시적 시험 상태 |
| C03a | 망치 | 동일 base speed4 검/망치 | 4초 공격/공격 중 도구 교체 | 검13/망치10·우회 없음·1.25 넉백 유지 | 통과 | e1472e918243; 실제 FixedUpdate 검사 |
| C04a | 방패 | 좌우·장착/없음 | 합성 우클릭 및 준비된 피해 방향 | 전방 일부/후방 일반 방어/미상 제외·해제 | 통과 | 557e6163fdd5; 실제 몬스터 공격은 별도 |
| C05a | 활 | 화살 장착/없음 | 실제 합성 당김·해제/지형·적 충돌/회수 | 커서 카메라/소모1/중력/피해1/회수 호환 | 통과 | c85d6bdc68c8,7c8075ad88ce; 충돌 위치 준비 |
| C06a | 박쥐/슬라임 | 양 prefab·장애물/지역 준비 | retreat/contact/death/영역 이탈 | 회피·쿨타임·천장복귀/피해·넉백·드롭/영역 복구 | 통과 | e08c53b6f0fa,a8c620ca3211,8a6e16ad2c2a; 합성 지형 포함 |
| C07a | 돌 골렘 | 실제 동굴 spawn·플레이어 위치 준비 | 무기/pick/wake/5걸음/slam/throw/return/death | 선제 없음/pick13/25slam/돌 비행 무피해/착지20 한 번/체력유지/core1 | 통과 | a22c5d2da797,1fabe38efbbb |
| I01a | 가방 상한 | tier1/2/3 저장 | 마을→필드/85kg 경계 수량 | 30/55/85 동기화·초과 거부·수량 보존 | 통과 | 0a8826afd3a5,c56ae0f249d3,b1d0c29e9467,ae4212af09fd |
| I02a | 보관함 드래그 | 마을 실제 UI·재료 | 양방향/무효drop/재열기/저장 | 총량 보존·실패 복귀·출처·가방 변환 | 통과 | a10060b3b3fd; 합성 포인터/실제 raycast |
| I03a | 레시피 그래프 | 59 발견 상태 준비 | 실제 노드 갱신/검 Prepare/ReturnAll | 활성 레시피 모든 재료 edge/재료 배치/비검색/숙련 불변 | 통과 | 92844050c282; 그래프 네이티브 드래그 미실행 |
| I04a | 각 설비 제작 | 재료/숙련 준비 | 톱·칼·숫돌/용광로·모루·담금질/작업대 | 2배수/수동성공실패/전액 재료환불·연료소비/품질·수집·저장 | 통과 | e76c3e851ca0,a1ab5adf64e8,a9efd7f11beb,c04fbb5b0109 |
| I05a | 장비 거치대/상자 | bag/chest 각 무기 | 실제 장착·해제·재열기·1920 render | Shield/Arrow/양손 숨김·출처·5/6열/검색없음 | 통과 | 4f602f3dc933; native 전체 비교 미실행 |
| E01a | 무기 상점 | 5000.5G·도구 재고 준비 | 구매/판매/drag cancel/AllConfirm 반복 | 가격·재고 정확/중복 없음/입력 복구 | 통과 | 11f65af57ce3; 모든 품목/가방 공간부족 조합 미실행 |
| E03a | 전당포 | 재료4개·소수G | 실제 판매→stock 구매/돈 부족/닫기 | 반값판매/원가격구매/재고·금액 보존/대출없음 | 통과 | 11f65af57ce3; 준비 재료 |
| U01a | 제물 | 준비 wood/branch·동화율 | 길게누르기/ESC/재열기/바치기 | 중복클릭 없음/취소 재료 보존/0효과 비활성/정확 감소·속도1 | 통과 | 1bc1e7f678db,6b4f400c7e8f |
| U02a | 사망 | 준비 가방·금화/치명피해 | 실제 ReturnTown | 가방 삭제/4% 금화/마을 지면·장비·상자·입력/동화율0 | 통과 | 6ae4176e7f0f; 피해 조건 준비 |
| U03a | 설정/메뉴 | 정상 플레이/격리 prefs | 4탭/슬라이더/닫기·재열기/수동 slot1 | 설정 수치 저장·닫기 입력1/슬롯 저장로드 | 통과 | 9b85b214a6ae; 앱 재시작 모든 설정 조합 미실행 |
| U04a | 워프 지도 | 실제 treasure/warp 준비 | 해금·지도 cancel/reopen·warp·일반지도 | 집1개/해금1회/목적지·입력복구/일반지도에 집 없음 | 통과 | 52dadf52184a; 중복 수정 동일 재검증 |
| U05a | Editor God Mode | 정상 catalog206/recipe59 | 2회/씬왕복/자동수동pause종료 | 99/발견1/중복 없음/일반 저장5개 보존 | 통과 | 9c950832886f; 도메인재로드·invalid catalog 미실행 |
| B01a | Windows 빌드 | 최종 소스의 격리 복사본 | Normal/Development BuildPlayer | 0errors/God타입 제외 | 통과 | 3216ad9fcd53; 각 warnings6 |
| V01a | 실제 화면 | 일반 빌드 독립 빈 저장 | 저장목록/Back/Play/Next/Skip/Cancel 클릭 | 표시·8빈행·대사변화·취소복귀/겹침 없음 | 통과 | 최신 native-captures; 사용자 저장 해시 불변 |
| V01b | 전체 정상 도보 플레이 | 키보드 자동화 필요 | Space/F/이동→제작·채집·수면·엔딩 | 실제 키와 안내만으로 전체 완료 | 환경상 확인 불가 | native Space 미전달; 원인 미확정. 합성 검사로 대체했다고 주장하지 않음 |
| V02a | 새 사용자 사용성 | 실제 신규 사용자 참가 | 처음부터 전체 플레이 | 안내만 보고 진행 | 미실행 | 신규 사용자 참여 없음 |


추가 개별 실행 — 2026-10-10 (52개 선택 검사; 전체 완료 아님)

| ID | 기능·진입 | 사전 상태 | 조작 | 기대 데이터 | 상태 | 근거·남은 범위 |
|---|---|---|---|---|---|---|
| D05a | 칼날 보완/실제 숫돌 진입 | 원목·판자0 준비/자루 제작 | 대화→진입→GrantOnce 재호출/저장 | 칼날1·applied·자루/반복불변 | 통과 | 30ed7aee9aa5; 자연 실패로0이 된 검사 아님 |
| D06a | 박덕수 물품 회수 | 일반재료·필수품 준비 | 실제문→대화→재회수 | 원목/생가죽만 제거·핵/칼날/자루/상자 보존 | 통과 | 32db4ffa7418; 모든 아이템 조합 미실행 |
| D09a | 납품 보완 | 귀환 전 가지0 준비 | 실제귀환→가지납품→수면 | 보완1회/1개·진행막힘 없음/Day2·저장 | 통과 | 32db4ffa7418; 홀짝 전체 조합 미실행 |
| E02a | 시설 상점 | 실제마을/기존금화재고 | 실제상점진입/대화종료/재열기취소 | 안내·무거래·데이터불변/입력복구 | 통과 | 37c765b397aa; 직접Advance, 네이티브Space 아님 |
| E04a | 레거시 주간 빚 UI | story없음/Day8·현금0·재고0 준비 | Yes·No/Continue/Done/재열기 | 이월/경고1→2·동일주중복없음·저장/입력1 | 통과 | 37c765b397aa; 실제7일활동·출구자동진입 미실행 |

## 추가 재시도 — 실제 실행 범위만 판정

| 기능 | 상태 | 실제 근거 |
|---|---|---|
| 1644aaae13a1 | 통과 | All eight actual title history rows individually confirmed and loaded; exact day/gold/fraction and town/input checked each. Eight independently reseeded prepared histories per load, not native pointer. |
| c56ad9b61441 | 통과 | Editor God Mode grant/service, catalog 99/discovery not mastery, second grant prevention, town-field-smithy, autosave/manual/pause isolated; native IMGUI button click/build/domain reload remain separate |
| 33376ed15dc5 | 통과 | Actual InputSystem walking/F boundary cancel-confirm/forest spawn/number-key axe/held mouse tree depletion/automatic drop pickup/return walking-F/inventory preservation. Completed-tutorial save with health100 prepared; no actor teleport, resource Interact call, injected damage or bag grant. Synthetic input and pointer event, not OS-native keys; no deer kill, full new-player story or sleep. |
| 5b1536719d6c | 통과 | Actual InputSystem walking/F boundary cancel-confirm/forest spawn/number-key axe/held mouse tree depletion/automatic drop pickup/return walking-F/inventory preservation. Completed-tutorial save with health100 prepared; no actor teleport, resource Interact call, injected damage or bag grant. Synthetic input and pointer event, not OS-native keys; actual mouse sword deer kill and automatic raw leather pickup included; full new-player story or sleep excluded. |
| 9db99a658e9d | 통과 | Actual town storage both-direction pointer transfers followed by closing UI mid-drag, owner/preview cleanup, exact quantities, reopen/save/export; prepared completed tutorial state, native drag remains separate |
| 43deaf4373a7 | 통과 | God Mode defers a requested assembly reload during this Editor-only session, preserves live services/99 grant/one-click flag and ordinary files; unlock on Play exit is separately checked after teardown. Does not claim general hot-reload support or actual crash verification. |
| settings-resume-42ddad314aae | 통과 | Fresh Unity process using Run_9b85b214a6ae reads prior actual SettingsProbe saved PlayerPrefs without re-seeding; title settings volumes/display/autoSave/mutes and repeated load verified. Synthetic UI click; no binding remap/native input or every possible setting combination claimed. |
| b77be7613aae | 통과 | Actual offering pointer hold/release suppression, synthetic Escape close with exact speed/input restoration and full material preservation; reopen/click/commit exact count and assimilation. Prepared wood/assimilation, field time accumulation disabled only in disposable test, no native F/ESC or natural statue encounter. |
| 정상 튜토리얼 숲 석상 도달 | 실패 | 09e2cb493b13: 숲 석상 부재·동굴 입력 차단. 이전 준비 상태 검사는 이 경로를 검증하지 않음 |

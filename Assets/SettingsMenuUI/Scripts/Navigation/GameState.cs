// [코드 지도] GameState: 게임 상태의 종류를 선언한다.
// 주요 함수: 필드·데이터 선언
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/SettingsMenuUI/Scripts/Navigation/GameState.cs.md

namespace SettingsMenuUI
{
    public enum GameState
    {
        Loading,
        MainMenu,
        Playing,
        Dead
    }
}

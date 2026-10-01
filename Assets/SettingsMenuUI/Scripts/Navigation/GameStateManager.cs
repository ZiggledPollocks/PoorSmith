// [코드 지도] GameStateManager: 게임·메뉴 상태 전환과 시간 정지를 관리한다.
// 주요 함수: SetState, Configure, Awake
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/SettingsMenuUI/Scripts/Navigation/GameStateManager.cs.md

using System;
using UnityEngine;

namespace SettingsMenuUI
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class GameStateManager : MonoBehaviour
    {
        [SerializeField] private GameState initialState = GameState.Loading;

        private GameState currentState;
        private bool initialized;

        public GameState CurrentState => initialized ? currentState : initialState;
        public event Action<GameState> OnGameStateChanged;

        private void Awake()
        {
            currentState = initialState;
            initialized = true;
        }

        public void Configure(GameState startingState)
        {
            initialState = startingState;
            if (Application.isPlaying)
            {
                SetState(startingState);
            }
        }

        public void SetState(GameState newState)
        {
            if (initialized && currentState == newState)
            {
                return;
            }

            currentState = newState;
            initialized = true;
            OnGameStateChanged?.Invoke(newState);
        }
    }
}

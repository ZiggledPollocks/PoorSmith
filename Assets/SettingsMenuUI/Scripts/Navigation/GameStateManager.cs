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

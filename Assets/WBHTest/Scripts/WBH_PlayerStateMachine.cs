using System;
using UnityEngine;

public class WBH_PlayerStateMachine : MonoBehaviour
{
    public PlayerState CurrentState { get; private set; }

    public event Action<PlayerState> OnEnterState;
    public event Action<PlayerState> OnExitState;
    public event Action<PlayerState, PlayerState> OnStateChanged;

    private void Awake()
    {
        CurrentState = PlayerState.Idle;
    }

    public bool Is(PlayerState state)
    {
        return CurrentState == state;
    }

    public void ChangeState(PlayerState next)
    {
        if (CurrentState == next)
            return;

        PlayerState previous = CurrentState;

        OnExitState?.Invoke(previous);

        CurrentState = next;

        OnEnterState?.Invoke(CurrentState);

        OnStateChanged?.Invoke(previous, CurrentState);
    }
}

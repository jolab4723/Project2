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

    // 현재 상태가 매개변수의 상태배열에 포함되는 상태인지 검사 
    public bool IsAnyState(params PlayerState[] states)
    {
        foreach (PlayerState state in states)
        {
            if (CurrentState == state)
                return true;
        }

        return false;
    }
}

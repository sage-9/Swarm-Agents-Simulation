using System;
using UnityEngine;

/// <summary>
/// Generic state machine implementation for managing agent state transitions.
/// Provides centralized state management and change notifications.
/// </summary>
/// <typeparam name="T">Enum type defining possible states</typeparam>
public class StateMachine<T> where T : System.Enum
{
    private T _currentState;
    public T CurrentState => _currentState;

    /// <summary>
    /// Fired when the state changes. Provides previous and new state.
    /// </summary>
    public event Action<T, T> OnStateChanged;

    /// <summary>
    /// Creates a state machine with the given initial state.
    /// </summary>
    public StateMachine(T initialState)
    {
        _currentState = initialState;
    }

    /// <summary>
    /// Transitions to a new state if it differs from current state.
    /// </summary>
    public void TransitionTo(T newState)
    {
        if (Equals(_currentState, newState)) return;

        T previousState = _currentState;
        _currentState = newState;

        OnStateChanged?.Invoke(previousState, newState);
    }

    /// <summary>
    /// Checks if currently in the given state.
    /// </summary>
    public bool IsInState(T state)
    {
        return Equals(_currentState, state);
    }
}

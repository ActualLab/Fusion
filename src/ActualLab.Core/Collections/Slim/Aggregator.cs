namespace ActualLab.Collections.Slim;

/// <summary>
/// A delegate that aggregates a single argument into a mutable state by reference.
/// </summary>
public delegate void Aggregator<TState, in TArg>(ref TState state, TArg arg);

/// <summary>
/// A delegate that aggregates two arguments into a mutable state by reference.
/// </summary>
public delegate void Aggregator<TState, in TArg1, in TArg2>(ref TState state, TArg1 arg1, TArg2 arg2);

/// <summary>
/// A delegate that aggregates three arguments into a mutable state by reference.
/// </summary>
public delegate void Aggregator<TState, in TArg1, in TArg2, in TArg3>(
    ref TState state, TArg1 arg1, TArg2 arg2, TArg3 arg3);

/// <summary>
/// A delegate that aggregates four arguments into a mutable state by reference.
/// </summary>
public delegate void Aggregator<TState, in TArg1, in TArg2, in TArg3, in TArg4>(
    ref TState state, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4);

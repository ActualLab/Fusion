namespace ActualLab.Fusion.Blazor.Authentication;

/// <summary>
/// A UI command representing an authentication state change,
/// used to track auth state transitions via the UI action tracker.
/// </summary>
public sealed class ChangeAuthStateUICommand : ICommand<AuthState>;

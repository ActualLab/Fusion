using Microsoft.AspNetCore.Components.Authorization;
using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.UI.Services;

/// <summary>
/// Extends <see cref="AuthenticationState"/> with the local <see cref="User"/> model
/// and forced sign-out status.
/// </summary>
public class AuthState(User? user, bool isSignOutForced = false)
    : AuthenticationState(user.OrGuest().ToClaimsPrincipal())
{
    public new User? User { get; } = user;
    public bool IsSignOutForced { get; } = isSignOutForced;

    public AuthState() : this(null)
    { }

    public virtual bool IsIdenticalTo(AuthState? other)
    {
        if (other?.GetType() != GetType())
            return false;

        return User == other.User && IsSignOutForced == other.IsSignOutForced;
    }
}

/// <summary>
/// A UI command representing an authentication state change.
/// </summary>
public sealed class ChangeAuthStateUICommand : ICommand<AuthState>;

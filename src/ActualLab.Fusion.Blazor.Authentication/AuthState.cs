using Microsoft.AspNetCore.Components.Authorization;
using ActualLab.Fusion.Authentication;

namespace ActualLab.Fusion.Blazor.Authentication;

public class AuthState(User? user, bool isSignOutForced = false)
    : AuthenticationState(user.OrGuest().ToClaimsPrincipal())
{
    public new User? User { get; } = user;
    public bool IsSignOutForced { get; } = isSignOutForced;

    public AuthState() : this(null)
    { }

    // Overriding equality for AuthState is undesirable, since its
    // base type (AuthenticationState) uses by-ref equality.
    // So we have to add an extra method for structural equality.
    public virtual bool IsIdenticalTo(AuthState? other)
    {
        if (other?.GetType() != GetType())
            return false;

        return User == other.User && IsSignOutForced == other.IsSignOutForced;
    }
}

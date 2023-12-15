using Microsoft.AspNetCore.Components.Authorization;
using ActualLab.Fusion.Authentication;

namespace ActualLab.Fusion.Blazor.Authentication;

public class AuthState(User? user, bool isSignOutForced = false)
    : AuthenticationState(user.OrGuest().ToClaimsPrincipal())
{
    public new User? User { get; } = user;
    public bool IsSignOutForced { get; } = isSignOutForced;

    public AuthState() : this(null) { }
}

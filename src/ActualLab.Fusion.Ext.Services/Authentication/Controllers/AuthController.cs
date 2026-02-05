#if !NETSTANDARD

using Microsoft.AspNetCore.Mvc;
using ActualLab.Fusion.Authentication.Endpoints;

namespace ActualLab.Fusion.Authentication.Controllers;

/// <summary>
/// MVC controller that handles sign-in and sign-out HTTP requests
/// by delegating to <see cref="AuthEndpoints"/>.
/// </summary>
public sealed class AuthController(AuthEndpoints handler) : Controller
{
    [HttpGet("~/signIn")]
    [HttpGet("~/signIn/{scheme}")]
    public Task SignIn(string? scheme = null, string? returnUrl = null)
        => handler.SignIn(HttpContext, scheme, returnUrl);

    [HttpGet("~/signOut")]
    public Task SignOut(string? scheme = null, string? returnUrl = null)
        => handler.SignOut(HttpContext, scheme, returnUrl);
}

#endif

using Microsoft.AspNetCore.Mvc;
using ActualLab.Fusion.Server.Endpoints;

namespace ActualLab.Fusion.Server.Controllers;

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

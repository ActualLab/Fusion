using Microsoft.AspNetCore.Mvc;
using ActualLab.Fusion.Server.Endpoints;

namespace ActualLab.Fusion.Server.Controllers;

/// <summary>
/// MVC controller that handles Blazor render mode selection via cookie.
/// </summary>
[Route("~/fusion/renderMode")]
public sealed class RenderModeController(RenderModeEndpoint handler) : ControllerBase
{
    [HttpGet]
    [HttpGet("{renderMode}")]
    public async Task<IActionResult> Invoke(string? renderMode, string? redirectTo = null)
    {
        var result = await handler.Invoke(HttpContext, renderMode, redirectTo).ConfigureAwait(false);
        return Redirect(result.Url);
    }
}

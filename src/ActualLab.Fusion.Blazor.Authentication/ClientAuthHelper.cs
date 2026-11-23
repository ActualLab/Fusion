using Microsoft.JSInterop;
using ActualLab.Fusion.Authentication;
using ActualLab.Resilience;

namespace ActualLab.Fusion.Blazor.Authentication;

/// <summary>
/// Client-side helper for performing sign-in, sign-out, and session management
/// operations via JavaScript interop and the <see cref="IAuth"/> service.
/// </summary>
public class ClientAuthHelper(IServiceProvider services) : IHasServices
{
    public static string SchemasJavaScriptExpression { get; set; } = "window.FusionAuth.schemas";

    protected (string Schema, string SchemaName)[]? CachedSchemas { get; set; }

    public IServiceProvider Services { get; } = services;
    public IAuth Auth => field ??= Services.GetRequiredService<IAuth>();
    public ISessionResolver SessionResolver => field ??= Services.GetRequiredService<ISessionResolver>();
    public Session Session => SessionResolver.Session;
    public ICommander Commander => field ??= Services.Commander();
    public IJSRuntime JSRuntime => field ??= Services.GetRequiredService<IJSRuntime>();

    public virtual async ValueTask<(string Name, string DisplayName)[]> GetSchemas()
    {
        if (CachedSchemas is not null)
            return CachedSchemas;

        string sSchemas;
        try {
            sSchemas = await JSRuntime
                .InvokeAsync<string>("eval", SchemasJavaScriptExpression)
                .ConfigureAwait(false); // The rest of this method doesn't depend on Blazor
        }
        catch (Exception e) when (e.IsServiceProviderDisposedException()) {
            return [];
        }

        var lSchemas = ListFormat.Default.Parse(sSchemas);
        var schemas = new (string, string)[lSchemas.Count / 2];
        for (int i = 0, j = 0; i < schemas.Length; i++, j += 2)
            schemas[i] = (lSchemas[j], lSchemas[j + 1]);
        CachedSchemas = schemas;
        return CachedSchemas;
    }

    // SignIn and SignOut do nothing once the page they'd run in is gone (e.g., a Blazor Server circuit)
    public virtual async ValueTask SignIn(string? schema = null)
    {
        try {
            await JSRuntime.InvokeVoidAsync("FusionAuth.signIn", schema ?? "").ConfigureAwait(false);
        }
        catch (Exception e) when (e.IsServiceProviderDisposedException()) {
            // Intended
        }
    }

    public virtual async ValueTask SignOut()
    {
        try {
            await JSRuntime.InvokeVoidAsync("FusionAuth.signOut").ConfigureAwait(false);
        }
        catch (Exception e) when (e.IsServiceProviderDisposedException()) {
            // Intended
        }
    }

    public virtual Task SignOut(Session session, bool force = false)
        => Commander.Call(new Auth_SignOut(session, force));
    public virtual Task SignOutEverywhere(bool force = true)
        => Commander.Call(new Auth_SignOut(Session, force) { KickAllUserSessions = true });
    public virtual Task Kick(Session session, string otherSessionHash, bool force = false)
        => Commander.Call(new Auth_SignOut(session, otherSessionHash, force));
}

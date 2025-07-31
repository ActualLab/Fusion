using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;
using ActualLab.Fusion.Authentication;

namespace ActualLab.Fusion.Blazor.Authentication;

public class ClientAuthHelper(IServiceProvider services) : IHasServices
{
    public static string SchemasJavaScriptExpression { get; set; } = "window.FusionAuth.schemas";

    protected (string Schema, string SchemaName)[]? CachedSchemas { get; set; }

    public IServiceProvider Services { get; } = services;
    [field: AllowNull, MaybeNull]
    public IAuth Auth => field ??= Services.GetRequiredService<IAuth>();
    [field: AllowNull, MaybeNull]
    public ISessionResolver SessionResolver => field ??= Services.GetRequiredService<ISessionResolver>();
    public Session Session => SessionResolver.Session;
    [field: AllowNull, MaybeNull]
    public ICommander Commander => field ??= Services.Commander();
    [field: AllowNull, MaybeNull]
    public IJSRuntime JSRuntime => field ??= Services.GetRequiredService<IJSRuntime>();

    public virtual async ValueTask<(string Name, string DisplayName)[]> GetSchemas()
    {
        if (CachedSchemas is not null)
            return CachedSchemas;

        var sSchemas = await JSRuntime
            .InvokeAsync<string>("eval", SchemasJavaScriptExpression)
            .ConfigureAwait(false); // The rest of this method doesn't depend on Blazor
        var lSchemas = ListFormat.Default.Parse(sSchemas);
        var schemas = new (string, string)[lSchemas.Count / 2];
        for (int i = 0, j = 0; i < schemas.Length; i++, j += 2)
            schemas[i] = (lSchemas[j], lSchemas[j + 1]);
        CachedSchemas = schemas;
        return CachedSchemas;
    }

    public virtual ValueTask SignIn(string? schema = null)
        => JSRuntime.InvokeVoidAsync("FusionAuth.signIn", schema ?? "");

    public virtual ValueTask SignOut()
        => JSRuntime.InvokeVoidAsync("FusionAuth.signOut");
    public virtual Task SignOut(Session session, bool force = false)
        => Commander.Call(new Auth_SignOut(session, force));
    public virtual Task SignOutEverywhere(bool force = true)
        => Commander.Call(new Auth_SignOut(Session, force) { KickAllUserSessions = true });
    public virtual Task Kick(Session session, string otherSessionHash, bool force = false)
        => Commander.Call(new Auth_SignOut(session, otherSessionHash, force));
}

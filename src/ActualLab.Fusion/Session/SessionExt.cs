using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Fusion;

public static class SessionExt
{
    public static bool IsDefault([NotNullWhen(true)] this Session? session)
        => session == Session.Default;

    public static bool IsValid([NotNullWhen(true)] this Session? session)
        => session is not null && Session.Validator.Invoke(session);

    public static Session RequireValid(this Session? session)
        => session.IsValid()
            ? session!
            : throw new ArgumentOutOfRangeException(nameof(session));

    public static Session ResolveDefault(this Session? session, ISessionResolver sessionResolver)
        => session.IsDefault() ? sessionResolver.Session : session.RequireValid();

    public static Session ResolveDefault(this Session? session, IServiceProvider services)
    {
        if (!session.IsDefault())
            return session.RequireValid();

        var sessionResolver = services.GetRequiredService<ISessionResolver>();
        return sessionResolver.Session;
    }
}

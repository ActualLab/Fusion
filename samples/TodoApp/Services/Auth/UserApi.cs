using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.Services.Auth;

/// <summary>
/// Client-facing implementation of <see cref="IUserApi"/>.
/// </summary>
public class UserApi(IServiceProvider services) : IUserApi, ISessionValidator
{
    private ISessionBackend SessionBackend { get; } = services.GetRequiredService<ISessionBackend>();
    private IUserBackend UserBackend { get; } = services.GetRequiredService<IUserBackend>();

    // IUserApi

    public virtual async Task<User?> GetOwn(
        Session session, CancellationToken cancellationToken = default)
    {
        var sessionInfo = await SessionBackend.GetSessionInfo(session, cancellationToken).ConfigureAwait(false);
        if (sessionInfo is null || !sessionInfo.IsAuthenticated())
            return null;

        return await UserBackend.Get(sessionInfo.UserId, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<ImmutableArray<SessionInfo>> ListOwnSessions(
        Session session, CancellationToken cancellationToken = default)
    {
        var user = await GetOwn(session, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return ImmutableArray<SessionInfo>.Empty;

        return await SessionBackend.GetUserSessions(user.Id, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdatePresence(
        Session session, CancellationToken cancellationToken = default)
        => SessionBackend.UpdatePresence(session, cancellationToken);

    public virtual Task OnSignOut(
        User_SignOut command, CancellationToken cancellationToken = default)
    {
        var backendCommand = new SessionBackend_SignOut(command.Session, command.KickUserSessionHash, command.KickAllUserSessions, command.Force);
        return services.Commander().Call(backendCommand, true, cancellationToken);
    }

    // ISessionValidator

    public async Task<bool> IsValidSession(
        Session session, CancellationToken cancellationToken = default)
    {
        if (!session.IsValid())
            return false;

        var sessionInfo = await SessionBackend.GetSessionInfo(session, cancellationToken).ConfigureAwait(false);
        return sessionInfo is null || !sessionInfo.IsSignOutForced;
    }
}

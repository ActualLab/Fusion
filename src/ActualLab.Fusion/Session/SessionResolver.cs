using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

#pragma warning disable CA1721, VSTHRD104

/// <summary>
/// Resolves the current <see cref="Session"/> for a DI scope,
/// typically used to obtain the session in service implementations.
/// </summary>
public interface ISessionResolver : IHasServices
{
    public Task<Session> SessionTask { get; }
    public bool HasSession { get; }
    public Session Session { get; set; }

    public Task<Session> GetSession(CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="ISessionResolver"/> that stores the session in an async task source
/// and requires a scoped service provider for mutation.
/// </summary>
public class SessionResolver(IServiceProvider services) : ISessionResolver
{
    protected readonly AsyncTaskMethodBuilder<Session> SessionSource = AsyncTaskMethodBuilderExt.New<Session>();

    public IServiceProvider Services { get; } = services;
    public Task<Session> SessionTask => SessionSource.Task;
    public bool HasSession => SessionTask.IsCompleted;

    public Session Session {
        get => HasSession
            ? SessionTask.GetAwaiter().GetResult()
            : throw ActualLab.Internal.Errors.NotInitialized(nameof(Session));
        set {
            if (!Services.IsScoped())
                throw Errors.SessionResolverSessionCannotBeSetForRootInstance();

            SessionSource.TrySetResult(value.Require());
        }
    }

    public virtual Task<Session> GetSession(CancellationToken cancellationToken = default)
        => SessionTask.WaitAsync(cancellationToken);
}

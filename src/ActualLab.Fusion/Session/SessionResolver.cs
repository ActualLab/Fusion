using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

#pragma warning disable CA1721, VSTHRD104

public interface ISessionResolver : IHasServices
{
    public Task<Session> SessionTask { get; }
    public bool HasSession { get; }
    public Session Session { get; set; }

    public Task<Session> GetSession(CancellationToken cancellationToken = default);
}

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

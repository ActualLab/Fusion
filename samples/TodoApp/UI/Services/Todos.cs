using System.Diagnostics;
using ActualLab.Diagnostics;
using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.UI.Services;

public class Todos(Session session, ITodoApi todoApi) : IComputeService, IDisposable, IHasIsDisposed
{
    private static readonly ActivitySource ActivitySource = AppInstruments.ActivitySource;
    private volatile int _isDisposed;

    public Session Session { get; } = session;
    public bool IsDisposed => _isDisposed != 0;

    public void Dispose()
        => Interlocked.Exchange(ref _isDisposed, 1);

    [ComputeMethod]
    public virtual async Task<TodoItem?> Get(Ulid id, CancellationToken cancellationToken = default)
    {
        using var _ = ActivitySource.StartActivity(typeof(Todos));
        return await todoApi.Get(Session, id, cancellationToken).ConfigureAwait(false);
    }

    [ComputeMethod]
    public virtual async Task<Ulid[]> ListIds(int count, CancellationToken cancellationToken = default)
    {
        using var _ = ActivitySource.StartActivity(typeof(Todos));
        return await todoApi.ListIds(Session, count, cancellationToken).ConfigureAwait(false);
    }

    [ComputeMethod]
    public virtual async Task<TodoItem[]> List(int count, CancellationToken cancellationToken = default)
    {
        using var _ = ActivitySource.StartActivity(typeof(Todos));
        var ids = await todoApi.ListIds(Session, count, cancellationToken).ConfigureAwait(false);
        var items = await ids
            .Select(id => todoApi.Get(Session, id, cancellationToken))
            .Collect(cancellationToken) // Like Task.WhenAll, but acting on IEnumerable<T>
            .ConfigureAwait(false);
        return items.SkipNullItems().ToArray();
    }

    [ComputeMethod]
    public virtual async Task<TodoSummary> GetSummary(CancellationToken cancellationToken = default)
    {
        using var _ = ActivitySource.StartActivity(typeof(Todos));
        return await todoApi.GetSummary(Session, cancellationToken).ConfigureAwait(false);
    }
}

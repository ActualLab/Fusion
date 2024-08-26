using System.Diagnostics;
using ActualLab.DependencyInjection;
using ActualLab.Diagnostics;
using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.UI.Services;

public class TodoUI(Session session, ITodoService todoService) : IComputeService, IDisposable, IHasIsDisposed
{
    private static readonly ActivitySource ActivitySource = AppInstruments.ActivitySource;
    private volatile int _isDisposed;

    public Session Session { get; } = session;
    public bool IsDisposed => _isDisposed != 0;

    public void Dispose()
        => Interlocked.Exchange(ref _isDisposed, 1);

    [ComputeMethod]
    public virtual async Task<Todo?> Get(Ulid id, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity(typeof(TodoUI));
        return await todoService.Get(Session, id, cancellationToken);
    }

    [ComputeMethod]
    public virtual async Task<Todo[]> List(int count, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity(typeof(TodoUI));
        var ids = await todoService.ListIds(Session, count, cancellationToken);
        var todos = await ids
            .Select(id => todoService.Get(Session, id, cancellationToken))
            .Collect(); // Like Task.WhenAll, but acting on IEnumerable<T>
        return todos.SkipNullItems().ToArray();
    }

    [ComputeMethod]
    public virtual async Task<TodoSummary> GetSummary(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity(typeof(TodoUI));
        return await todoService.GetSummary(Session, cancellationToken);
    }
}

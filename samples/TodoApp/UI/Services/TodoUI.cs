using ActualLab.DependencyInjection;
using Templates.TodoApp.Abstractions;

namespace Templates.TodoApp.UI.Services;

public class TodoUI(Session session, ITodoService todoService) : IComputeService, IDisposable, IHasIsDisposed
{
    private volatile int _isDisposed;

    public Session Session { get; } = session;
    public bool IsDisposed => _isDisposed != 0;

    public void Dispose()
        => Interlocked.Exchange(ref _isDisposed, 1);

    [ComputeMethod]
    public virtual Task<Todo?> Get(Ulid id, CancellationToken cancellationToken = default)
        => todoService.Get(Session, id, cancellationToken);

    [ComputeMethod]
    public virtual async Task<Todo[]> List(int count, CancellationToken cancellationToken = default)
    {
        var ids = await todoService.ListIds(Session, count, cancellationToken);
        var todos = await ids
            .Select(id => todoService.Get(Session, id, cancellationToken))
            .Collect(); // Like Task.WhenAll, but acting on IEnumerable<T>
        return todos.SkipNullItems().ToArray();
    }

    [ComputeMethod]
    public virtual Task<TodoSummary> GetSummary(CancellationToken cancellationToken = default)
        => todoService.GetSummary(Session, cancellationToken);
}

using Stl.Fusion.Extensions;

namespace Templates.TodoApp.Abstractions;

public record Todo(string Id, string Title, bool IsDone = false)
{
    public Todo() : this("", "") { }
}

public record TodoSummary(int Count, int DoneCount)
{
    public TodoSummary() : this(0, 0) { }
}

public record AddOrUpdateTodoCommand(Session Session, Todo Item) : ISessionCommand<Todo>
{
    // Newtonsoft.Json needs this constructor to deserialize this record
    public AddOrUpdateTodoCommand() : this(Session.Null, default!) { }
}

public record RemoveTodoCommand(Session Session, string Id) : ISessionCommand<Unit>
{
    // Newtonsoft.Json needs this constructor to deserialize this record
    public RemoveTodoCommand() : this(Session.Null, "") { }
}

public interface ITodoService
{
    // Commands
    [CommandHandler]
    Task<Todo> AddOrUpdate(AddOrUpdateTodoCommand command, CancellationToken cancellationToken = default);
    [CommandHandler]
    Task Remove(RemoveTodoCommand command, CancellationToken cancellationToken = default);

    // Queries
    [ComputeMethod]
    Task<Todo?> Get(Session session, string id, CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<Todo[]> List(Session session, PageRef<string> pageRef, CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<TodoSummary> GetSummary(Session session, CancellationToken cancellationToken = default);
}

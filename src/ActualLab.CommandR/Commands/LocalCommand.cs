using ActualLab.CommandR.Internal;

namespace ActualLab.CommandR.Commands;

public interface ILocalCommand : ICommand
{
    Task Run(CancellationToken cancellationToken);
}

public abstract record LocalCommand : ILocalCommand
{
    public Ulid CommandId { get; init; } = Ulid.NewUlid();
    public string Title { get; init; } = "";

    public static LocalActionCommand New(Func<CancellationToken, Task> handler)
        => new() { Handler = handler };

    public static LocalActionCommand New(Action handler)
        => new() {
            Handler = _ => {
                handler();
                return Task.CompletedTask;
            }
        };

    public static LocalFuncCommand<T> New<T>(Func<CancellationToken, Task<T>> handler)
        => new() { Handler = handler };

    public static LocalFuncCommand<T> New<T>(Func<T> handler)
        => new() { Handler = _ => Task.FromResult(handler()) };

    public abstract Task Run(CancellationToken cancellationToken);
}

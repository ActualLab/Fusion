using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Operations.Internal;

namespace ActualLab.Fusion.Operations;

public interface ICompletion : ISystemCommand, IBackendCommand
{
    public Operation Operation { get; }
}

public interface ICompletion<TCommand> : ICompletion
    where TCommand : class, ICommand;

public record Completion<TCommand>(Operation Operation) : ICompletion<TCommand>
    where TCommand : class, ICommand;

public static class CompleteAsync
{
    // This is just to ensure the constructor accepting ICommand is "used",
    // because it is really used inside New, but via reflection.
#pragma warning disable CA1823
    private static readonly Completion<ICommand> DummyCompletion =
        new(new Operation() { Command = new DummyCommand() });
#pragma warning restore CA1823

    public static ICompletion New(Operation operation)
    {
        var command = (ICommand?)operation.Command
            ?? throw Errors.OperationHasNoCommand(nameof(operation));
        var tCompletion = typeof(Completion<>).MakeGenericType(command.GetType());
        var completion = (ICompletion)tCompletion.CreateInstance(operation);
        return completion;
    }

    // Nested types

    private sealed record DummyCommand : ICommand<Unit>;
}

using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Operations.Internal;

namespace ActualLab.Fusion.Operations;

/// <summary>
/// A system command representing the completion phase of an operation.
/// </summary>
public interface ICompletion : ISystemCommand, IBackendCommand
{
    public Operation Operation { get; }
}

/// <summary>
/// A strongly-typed <see cref="ICompletion"/> associated with a specific command type.
/// </summary>
public interface ICompletion<TCommand> : ICompletion
    where TCommand : class, ICommand;

/// <summary>
/// Default implementation of <see cref="ICompletion{TCommand}"/> carrying the completed operation.
/// </summary>
public record Completion<TCommand>(Operation Operation) : ICompletion<TCommand>
    where TCommand : class, ICommand;

/// <summary>
/// Factory for creating <see cref="ICompletion"/> instances from an <see cref="Operation"/>.
/// </summary>
public static class Completion
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

using System;
using System.Reactive;
using Stl.CommandR;
using Stl.CommandR.Commands;
using Stl.Fusion.Authentication.Commands;
using Stl.Fusion.Operations.Internal;
using Stl.Reflection;

namespace Stl.Fusion.Operations
{
    public interface ICompletion : IServerSideCommand<Unit>, IMetaCommand
    {
        IOperation Operation { get; }
    }

    public interface ICompletion<out TCommand> : IMetaCommand<TCommand>, ICompletion
        where TCommand : class, ICommand
    { }

    public record Completion<TCommand>(TCommand Command, IOperation Operation)
        : ServerSideCommandBase<Unit>, ICompletion<TCommand>
        where TCommand : class, ICommand
    {
        #if NETSTANDARD2_0
        ICommand IMetaCommand.UntypedCommand => Command;
        #endif
        
        public Completion(IOperation operation)
            : this((TCommand?) operation.Command ?? throw Errors.OperationHasNoCommand(nameof(operation)), operation)
        { }
    }

    public static class Completion
    {
        // This is just to ensure the constructor accepting ICommand is "used",
        // because it is really used inside New, but via reflection.
        private static readonly Completion<ICommand> DummyCompletion =
            new(new TransientOperation() { Command = new SignOutCommand() });

        public static ICompletion New(IOperation operation)
        {
            var command = (ICommand?) operation.Command
                ?? throw Errors.OperationHasNoCommand(nameof(operation));
            var tCompletion = typeof(Completion<>).MakeGenericType(command.GetType());
            var completion = (ICompletion) tCompletion.CreateInstance(operation)!;
            return completion.MarkServerSide();
        }
    }
}

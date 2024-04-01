using ActualLab.Fusion.Operations.Internal;

namespace ActualLab.Fusion.Operations;

public static class OperationExt
{
    public static ClosedDisposable<(Operation, ImmutableList<NestedOperation>?)> SuppressNestedCommandLogging(
        this Operation operation)
    {
        var nestedCommands = operation.Items.Get<ImmutableList<NestedOperation>>();
        operation.Items.Remove<ImmutableList<NestedOperation>>();
        return Disposable.NewClosed(
            (operation, nestedCommands),
            state => state.operation.Items.Set(state.nestedCommands));
    }
}

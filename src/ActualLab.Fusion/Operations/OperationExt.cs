using ActualLab.Fusion.Operations.Internal;

namespace ActualLab.Fusion.Operations;

public static class OperationExt
{
    public static ClosedDisposable<(Operation, List<NestedCommand>)> SuppressNestedCommandLogging(
        this Operation operation)
    {
        var nestedCommands = operation.NestedCommands;
        operation.NestedCommands = new();
        return Disposable.NewClosed(
            (operation, nestedCommands),
            state => state.operation.NestedCommands = state.nestedCommands);
    }
}

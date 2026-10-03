using ActualLab.CommandR.Operations;
using ActualLab.Reflection;

namespace ActualLab.Fusion.Tests.OperationEvents;

// Reusable "must not throw" harness for the no-fail contract of operation-completion listeners
// (docs/tasks/invalidation-audit.md, item 1).
public static class OperationCompletionNoThrowTester
{
    public static async Task AssertCompletionListenersDoNotThrow(
        IServiceProvider services, Operation operation, CommandContext? commandContext = null)
    {
        var listeners = services.GetServices<IOperationCompletionListener>().ToList();
        listeners.Should().NotBeEmpty("the harness is pointless without at least one registered listener");
        foreach (var listener in listeners) {
            Func<Task> act = () => listener.OnOperationCompleted(operation, commandContext);
            await act.Should().NotThrowAsync(
                $"'{listener.GetType().GetName()}' must never throw on operation completion");
        }
    }
}

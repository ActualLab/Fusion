using ActualLab.CommandR.Operations;
using ActualLab.Reflection;

namespace ActualLab.Fusion.Tests.OperationEvents;

// Reusable "must not throw" harness for the no-fail contract of operation-completion listeners
// and the invalidation replay pass (docs/tasks/invalidation-audit.md, items 1 and 2).
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

    // Bypasses CompletionProducer's own try/catch by dispatching the completion command directly,
    // so a failure inside InvalidatingCommandCompletionHandler's replay pass fails the test.
    public static async Task AssertInvalidationPassDoesNotThrow(ICommander commander, Operation operation)
    {
        Func<Task> act = () => {
            var completion = Completion.New(operation);
            var context = CommandContext.New(commander, completion, isOutermost: true);
            return context.Call(CancellationToken.None);
        };
        await act.Should().NotThrowAsync("the invalidation replay pass must never throw");
    }
}

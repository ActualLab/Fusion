using ActualLab.CommandR.Operations;
namespace ActualLab.Fusion.Operations;

public interface IOperationCompletionNotifier
{
    public Task<bool> NotifyCompleted(Operation operation, CommandContext? commandContext);
}

public class OperationCompletionNotifier : IOperationCompletionNotifier
{
    public record Options
    {
        // Should be >= BatchSize @ DbOperationLogReader.Options
        public int MaxKnownOperationCount { get; init; } = 16384;
        // Should be >= max commit + processing time @ DbOperationLogReader.Options
        public TimeSpan MaxKnownOperationAge { get; init; } = TimeSpan.FromMinutes(15);
        public MomentClock? Clock { get; init; }
    }

    protected Options Settings { get; }
    protected IServiceProvider Services { get; }
    protected HostId HostId { get; }
    protected IOperationCompletionListener[] OperationCompletionListeners { get; }
    protected RecentlySeenMap<Symbol, Unit> RecentlySeenUuids { get; }
    protected object Lock => RecentlySeenUuids;
    protected MomentClock Clock { get; }
    protected ILogger Log { get; }

    public OperationCompletionNotifier(Options settings, IServiceProvider services)
    {
        Settings = settings;
        Services = services;
        Log = Services.LogFor(GetType());
        Clock = Settings.Clock ?? Services.Clocks().SystemClock;

        HostId = Services.GetRequiredService<HostId>();
        OperationCompletionListeners = Services.GetServices<IOperationCompletionListener>().ToArray();
        RecentlySeenUuids = new RecentlySeenMap<Symbol, Unit>(
            Settings.MaxKnownOperationCount,
            Settings.MaxKnownOperationAge,
            Clock);
    }

    public Task<bool> NotifyCompleted(Operation operation, CommandContext? commandContext)
    {
        lock (Lock) {
            if (!RecentlySeenUuids.TryAdd(operation.Uuid, operation.LoggedAt))
                return TaskExt.FalseTask;
        }

        using var _ = ExecutionContextExt.TrySuppressFlow();
        return Task.Run(async () => {
            var isLocal = commandContext != null;
            var isFromLocalAgent = string.Equals(operation.HostId, HostId.Id.Value, StringComparison.Ordinal);
            // An important assertion
            if (isLocal != isFromLocalAgent) {
                var message = isFromLocalAgent
                    ? "Assertion failed: operation w/o CommandContext originates from local agent"
                    : "Assertion failed: operation with CommandContext originates from another agent";
                // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                Log.LogError(message);
            }

            // Notification
            var listeners = OperationCompletionListeners;
            var tasks = new Task[listeners.Length];
            for (var i = 0; i < listeners.Length; i++) {
                var listener = listeners[i];
                try {
                    tasks[i] = listener.OnOperationCompleted(operation, commandContext);
                }
                catch (Exception e) {
                    tasks[i] = Task.CompletedTask;
                    Log.LogError(e, "Operation completion listener of type '{HandlerType}' failed",
                        listener.GetType());
                }
            }
            try {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (Exception e) {
                Log.LogError(e, "One of operation completion listeners failed");
            }
            return true;
        });
    }
}

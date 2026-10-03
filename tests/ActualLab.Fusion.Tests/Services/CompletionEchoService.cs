using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Operations;
using ActualLab.Fusion.Tests.DbModel;
using MessagePack;

namespace ActualLab.Fusion.Tests.Services;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record CompletionEcho_Set(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember, MemoryPackOrder(1), Key(1)] int Value
) : ICommand<Unit>;

// The escape hatch for a command whose invalidations can't all be declared up front: it stores its
// operation and leaves the rest to an ICompletion<TCommand> handler, which runs on every host.
[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public class CompletionEchoService(IServiceProvider services)
    : DbServiceBase<TestDbContext>(services), IComputeService
{
    private readonly ConcurrentDictionary<string, int> _values = new(StringComparer.Ordinal);

    [ComputeMethod]
    public virtual Task<int> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_values.GetValueOrDefault(key));

    [CommandHandler]
    public virtual async Task OnSet(CompletionEcho_Set command, CancellationToken cancellationToken = default)
    {
        var dbContext = await DbHub.CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
        await using var _ = dbContext.ConfigureAwait(false);

        _values[command.Key] = command.Value;
        // Nothing is deferred here, so the default store mode would be None - and then no other
        // host would ever see this operation
        CommandContext.GetCurrent().Operation.StoreMode = OperationStoreMode.Operation;
    }
}

// An application-style ICompletion<TCommand> handler: it sees the operation on the origin and on
// every host that reads its row. It has to be a filter - CompletionTerminator is the one
// non-filter handler of every completion, and a second one is an error.
public sealed class CompletionEchoCatcher : ICommandHandler<ICompletion<CompletionEcho_Set>>
{
    private readonly List<(string HostId, string Key)> _completions = [];

    public IReadOnlyList<(string HostId, string Key)> Completions {
        get {
            lock (_completions)
                return _completions.ToArray();
        }
    }

    [CommandFilter(Priority = 100)]
    public Task OnCommand(
        ICompletion<CompletionEcho_Set> command, CommandContext context, CancellationToken cancellationToken)
    {
        var operation = command.Operation;
        var key = ((CompletionEcho_Set)operation.Command!).Key;
        lock (_completions)
            _completions.Add((operation.HostId, key));
        return context.InvokeRemainingHandlers(cancellationToken);
    }
}

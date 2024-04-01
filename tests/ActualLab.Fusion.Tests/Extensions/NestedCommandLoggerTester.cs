using ActualLab.Fusion.Extensions;

namespace ActualLab.Fusion.Tests.Extensions;

public class NestedCommandLoggerTester(IKeyValueStore keyValueStore) : IComputeService
{
    private IKeyValueStore KeyValueStore { get; } = keyValueStore;

    [CommandHandler]
    public virtual async Task SetMany(NestedCommandLoggerTester_SetMany command, CancellationToken cancellationToken = default)
    {
        var (keys, valuePrefix) = command;
        var first = keys.FirstOrDefault();
        if (first == null)
            return;

        await KeyValueStore.Set(default, first, valuePrefix + keys.Length, cancellationToken);
        var nextCommand = new NestedCommandLoggerTester_SetMany(keys.Skip(1).ToArray(), valuePrefix);
        var commander = this.GetCommander();
        await commander.Call(nextCommand, cancellationToken).ConfigureAwait(false);
    }
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
// ReSharper disable once InconsistentNaming
public partial record NestedCommandLoggerTester_SetMany(
    [property: DataMember, MemoryPackOrder(0)] string[] Keys,
    [property: DataMember, MemoryPackOrder(1)] string ValuePrefix
) : ICommand<Unit>;

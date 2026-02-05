namespace ActualLab.Redis;

/// <summary>
/// A set of named sequences backed by a <see cref="RedisHash"/>,
/// supporting atomic increment with optional reset logic.
/// </summary>
public class RedisSequenceSet(RedisHash hash)
{
    public RedisHash Hash { get; } = hash;
    public long ResetRange { get; init; } = 1024;

    public async Task<long> Next(string key, long maxUsedValue = -1, long increment = 1)
    {
        var value = await Hash.Increment(key, increment).ConfigureAwait(false);
        if (maxUsedValue < 0)
            return value;
        if (maxUsedValue < value && value <= maxUsedValue + ResetRange)
            return value;
        await Reset(key, maxUsedValue).ConfigureAwait(false);
        value = await Hash.Increment(key, increment).ConfigureAwait(false);
        return value;
    }

    public Task Reset(string key, long value)
        => Hash.Set(key, value);

    public Task Clear()
        => Hash.Clear();
}

/// <summary>
/// A typed <see cref="RedisSequenceSet"/> scoped by <typeparamref name="TScope"/>
/// for multi-context dependency injection.
/// </summary>
public sealed class RedisSequenceSet<TScope>(RedisHash hash) : RedisSequenceSet(hash);

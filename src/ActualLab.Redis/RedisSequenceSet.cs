namespace ActualLab.Redis;

/// <summary>
/// A set of named sequences backed by a <see cref="RedisHash"/>,
/// supporting atomic increment with optional reset logic.
/// </summary>
public class RedisSequenceSet(RedisHash hash)
{
    private const string NextScript = """
        local function compare(a, b)
            local aNegative = string.sub(a, 1, 1) == '-'
            local bNegative = string.sub(b, 1, 1) == '-'
            if aNegative ~= bNegative then
                return aNegative and -1 or 1
            end
            local aDigits = aNegative and string.sub(a, 2) or a
            local bDigits = bNegative and string.sub(b, 2) or b
            if string.len(aDigits) ~= string.len(bDigits) then
                if aNegative then
                    return string.len(aDigits) > string.len(bDigits) and -1 or 1
                end
                return string.len(aDigits) < string.len(bDigits) and -1 or 1
            end
            if aDigits == bDigits then
                return 0
            end
            if aNegative then
                return aDigits > bDigits and -1 or 1
            end
            return aDigits < bDigits and -1 or 1
        end

        redis.call('HINCRBY', KEYS[1], ARGV[1], ARGV[2])
        local value = redis.call('HGET', KEYS[1], ARGV[1])
        if compare(ARGV[3], value) < 0 and compare(value, ARGV[4]) <= 0 then
            return value
        end
        redis.call('HSET', KEYS[1], ARGV[1], ARGV[3])
        redis.call('HINCRBY', KEYS[1], ARGV[1], ARGV[2])
        return redis.call('HGET', KEYS[1], ARGV[1])
        """;

    public RedisHash Hash { get; } = hash;
    public long ResetRange { get; init; } = 1024;

    public async Task<long> Next(string key, long maxUsedValue = -1, long increment = 1)
    {
        if (maxUsedValue < 0)
            return await Hash.Increment(key, increment).ConfigureAwait(false);

        var resetLimit = unchecked(maxUsedValue + ResetRange);
        var database = await Hash.RedisDb.Database.Get().ConfigureAwait(false);
        var result = await database.ScriptEvaluateAsync(
                NextScript,
                [Hash.HashKey],
                [key, increment, maxUsedValue, resetLimit])
            .ConfigureAwait(false);
        return (long)result;
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

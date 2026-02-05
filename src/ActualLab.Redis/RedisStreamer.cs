using StackExchange.Redis;
using Errors = ActualLab.Redis.Internal.Errors;

namespace ActualLab.Redis;

/// <summary>
/// Provides streaming read/write operations over a Redis Stream for typed
/// <typeparamref name="T"/> items, with pub/sub-based change notifications.
/// </summary>
public sealed class RedisStreamer<T>(RedisDb redisDb, string key, RedisStreamer<T>.Options? settings = null)
{
    /// <summary>
    /// Configuration options for <see cref="RedisStreamer{T}"/>.
    /// </summary>
    public record Options
    {
        public int MaxStreamLength { get; init; } = 2048;
        public string AppendPubKeySuffix { get; init; } = "-updates";
        public TimeSpan AppendCheckPeriod { get; init; } = TimeSpan.FromSeconds(1);
        public TimeSpan? AppendSubscribeTimeout { get; init; } = TimeSpan.FromSeconds(5);
        public TimeSpan? ExpirationPeriod { get; set; } = TimeSpan.FromHours(1);
        public IByteSerializer<T> Serializer { get; init; } = ByteSerializer<T>.Default;
        public ITextSerializer<ExceptionInfo> ErrorSerializer { get; init; } = TextSerializer<ExceptionInfo>.Default;
        public MomentClock Clock { get; init; } = MomentClockSet.Default.CpuClock;

        // You normally don't need to modify these
        public string ItemKey { get; init; } = "i";
        public string StatusKey { get; init; } = "s";
        public string StartedStatus { get; init; } = "[";
        public string EndedStatus { get; init; } = "]";
    }

    public Options Settings { get; } = settings ?? new ();
    public RedisDb RedisDb { get; } = redisDb;
    public string Key { get; } = key;

    public async IAsyncEnumerable<T> Read([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var appendSub = GetAppendSub();
        await using var _1 = appendSub.ConfigureAwait(false);
        await appendSub.Subscribe().ConfigureAwait(false);

        var position = (RedisValue)"0-0";
        var serializer = Settings.Serializer;
        var appendNotificationTask = appendSub.NextMessage();
        while (true) {
            cancellationToken.ThrowIfCancellationRequested(); // Redis doesn't support cancellation
            var database = await RedisDb.Database.Get(cancellationToken).ConfigureAwait(false);
            var entries = await database.StreamReadAsync(Key, position, 10).ConfigureAwait(false);
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (entries is null || entries.Length == 0) {
                var appendResult = await appendNotificationTask
                    .WaitResultAsync(Settings.Clock, Settings.AppendCheckPeriod, cancellationToken)
                    .ConfigureAwait(false);
                if (appendResult.HasValue)
                    appendNotificationTask = null;
#pragma warning disable CA2025 // Ensure tasks using 'IDisposable' instances complete before the instances are disposed
                appendNotificationTask = appendSub.NextMessage(appendNotificationTask);
#pragma warning restore CA2025
                continue;
            }

            foreach (var entry in entries) {
                var status = (string?)entry[Settings.StatusKey];
                if (!status.IsNullOrEmpty()) {
                    if (string.Equals(status, Settings.StartedStatus, StringComparison.Ordinal))
                        continue;
                    if (string.Equals(status, Settings.EndedStatus, StringComparison.Ordinal))
                        yield break;

                    var errorInfo = Settings.ErrorSerializer.Read(status!);
                    throw errorInfo.ToException() ?? Errors.SourceStreamError();
                }

                var data = (ReadOnlyMemory<byte>)entry[Settings.ItemKey];
                var item = serializer.Read(data, out _);
                yield return item;

                position = entry.Id;
            }
        }
    }

    public Task Write(
        IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
        => Write(source, _ => default, cancellationToken);

    public Task Write(
        IAsyncEnumerable<T> source,
        Action<RedisStreamer<T>> newStreamAnnouncer,
        CancellationToken cancellationToken = default)
        => Write(source,
            self => {
                newStreamAnnouncer(self);
                return default;
            },
            cancellationToken);

    public async Task Write(
        IAsyncEnumerable<T> source,
        Func<RedisStreamer<T>, ValueTask> newStreamAnnouncer,
        CancellationToken cancellationToken = default)
    {
        var database = await RedisDb.Database.Get(cancellationToken).ConfigureAwait(false);
        var appendPub = GetAppendPub();
        var error = (Exception?) null;
        var lastAppendTask = AppendStart(database, newStreamAnnouncer, appendPub, cancellationToken);
        if (Settings.ExpirationPeriod is { } expirationPeriod)
            await database.KeyExpireAsync(Key, expirationPeriod).ConfigureAwait(false);
        try {
            await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false)) {
                await lastAppendTask.ConfigureAwait(false);
                lastAppendTask = AppendItem(database, item, appendPub, cancellationToken);
            }
            await lastAppendTask.ConfigureAwait(false);
        }
        catch (Exception e) {
            error = e;
        }
        finally {
            if (!lastAppendTask.IsCompleted)
                try {
                    await lastAppendTask.ConfigureAwait(false);
                }
                catch (Exception e) {
                    error = e;
                }
            // No cancellation for AppendEnd - it should propagate it
            await AppendEnd(database, error, appendPub).ConfigureAwait(false);
        }
        if (error is not null)
            throw error;
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task AppendStart(
        IDatabase database,
        Func<RedisStreamer<T>, ValueTask> newStreamAnnouncer,
        RedisPub appendPub,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); // StackExchange.Redis doesn't support cancellation
        await database.StreamAddAsync(
                Key, Settings.StatusKey, Settings.StartedStatus,
                maxLength: Settings.MaxStreamLength,
                useApproximateMaxLength: true)
            .ConfigureAwait(false);
        await appendPub.Publish(RedisValue.EmptyString).ConfigureAwait(false);
        await newStreamAnnouncer(this).ConfigureAwait(false);
    }

    private async Task AppendItem(
        IDatabase database,
        T item,
        RedisPub appendPub,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); // StackExchange.Redis doesn't support cancellation
        using var bufferWriter = Settings.Serializer.Write(item);
        await database.StreamAddAsync(
                Key, Settings.ItemKey, bufferWriter.WrittenMemory,
                maxLength: Settings.MaxStreamLength,
                useApproximateMaxLength: true)
            .ConfigureAwait(false);
        await appendPub.Publish(RedisValue.EmptyString).ConfigureAwait(false);
    }

    private async Task AppendEnd(
        IDatabase database,
        Exception? error,
        RedisPub appendPub)
    {
        var finalStatus = Settings.EndedStatus;
        if (error is not null)
            finalStatus = Settings.ErrorSerializer.Write(error);
        await database.StreamAddAsync(
                Key, Settings.StatusKey, finalStatus,
                maxLength: Settings.MaxStreamLength,
                useApproximateMaxLength: true)
            .ConfigureAwait(false);
        await appendPub.Publish(RedisValue.EmptyString).ConfigureAwait(false);
    }

    public async Task Remove()
    {
        var database = await RedisDb.Database.Get().ConfigureAwait(false);
        await database.KeyDeleteAsync(Key, CommandFlags.FireAndForget).ConfigureAwait(false);
    }

    // Protected methods

    private RedisPub GetAppendPub()
        => RedisDb.GetPub(Key + Settings.AppendPubKeySuffix);

    private RedisTaskSub GetAppendSub()
        => RedisDb.GetTaskSub(
            (Key + Settings.AppendPubKeySuffix, RedisChannel.PatternMode.Literal),
            Settings.AppendSubscribeTimeout);
}

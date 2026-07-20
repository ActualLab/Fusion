using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Fusion.EntityFramework.Operations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ActualLab.Fusion.EntityFramework.Npgsql;

#pragma warning disable EF1002

/// <summary>
/// An <see cref="IDbLogWatcher{TDbContext, TDbEntry}"/> that uses PostgreSQL
/// LISTEN/NOTIFY to detect database log changes across hosts.
/// </summary>
public class NpgsqlDbLogWatcher<TDbContext, TDbEntry>(
    NpgsqlDbLogWatcherOptions<TDbContext> settings,
    IServiceProvider services
    ) : DbLogWatcher<TDbContext, TDbEntry>(services)
    where TDbContext : DbContext
{
    public NpgsqlDbLogWatcherOptions<TDbContext> Settings { get; } = settings;

    // Protected methods

    protected override DbShardWatcher CreateShardWatcher(string shard)
        => new ShardWatcher(this, shard);

    // Private methods

    private static string QuoteChannelName(string channelName)
        => new NpgsqlCommandBuilder().QuoteIdentifier(channelName);

    // Nested types

    /// <summary>
    /// A <see cref="DbShardWatcher"/> that listens for PostgreSQL notifications
    /// and sends NOTIFY commands to signal log changes.
    /// </summary>
    protected class ShardWatcher : DbShardWatcher
    {
        private readonly object _sendLock = new();
        private Task? _activeSendTask;
        private Task? _queuedSendTask;

        public NpgsqlDbLogWatcher<TDbContext, TDbEntry> Owner { get; }
        public DbHub<TDbContext> DbHub => Owner.DbHub;
        public string ListenSql { get; }
        public string NotifySql { get; }
        public string QuotedNotifyPayload { get; }
        public TDbContext? DbContext { get; set; }

        public ShardWatcher(NpgsqlDbLogWatcher<TDbContext, TDbEntry> owner, string shard)
            : base(shard)
        {
            Owner = owner;
            var hostId = DbHub.HostId;
            var channelName = owner.Settings.ChannelNameFormatter.Invoke(shard, typeof(TDbEntry));
            var quotedChannelName = QuoteChannelName(channelName);
            QuotedNotifyPayload = hostId.Id
#if NETSTANDARD2_0
                .Replace("'", "''");
#else
                .Replace("'", "''", StringComparison.Ordinal);
#endif
            ListenSql = $"LISTEN {quotedChannelName}";
            NotifySql = $"NOTIFY {quotedChannelName}, '{QuotedNotifyPayload}'";

            var watchChain = new AsyncChain($"Watch({shard})", async cancellationToken => {
                var dbContext = await DbHub.CreateDbContext(Shard, cancellationToken).ConfigureAwait(false);
                await using var _ = dbContext.ConfigureAwait(false);

                var database = dbContext.Database;
                await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
                var dbConnection = (NpgsqlConnection) database.GetDbConnection()!;
                dbConnection.Notification += (_, eventArgs) => {
                    if (!string.Equals(eventArgs.Payload, hostId.Id, StringComparison.Ordinal))
                        MarkChanged();
                };
                await dbContext.Database.ExecuteSqlRawAsync(ListenSql, cancellationToken).ConfigureAwait(false);
                while (!cancellationToken.IsCancellationRequested)
                    await dbConnection.WaitAsync(cancellationToken).ConfigureAwait(false);
            }).RetryForever(owner.Settings.TrackerRetryDelays, owner.Log);

            _ = watchChain.RunIsolated(StopToken);
        }

        protected override async Task DisposeAsyncCore()
        {
            Task? lastSendTask;
            lock (_sendLock)
                lastSendTask = _queuedSendTask ?? _activeSendTask;
            if (lastSendTask is not null)
                await lastSendTask.SilentAwait(false);
            if (DbContext is { } dbContext) {
                DbContext = null;
                await dbContext.DisposeAsync().ConfigureAwait(false);
            }
        }

        public override Task NotifyChanged(CancellationToken cancellationToken)
        {
            // Coalescing instead of a send queue: at most one send is in flight and at most one more
            // is queued behind it. The payload is constant (host id), so the queued send covers every
            // request accepted while it awaits its turn - they all share its task.
            Task sendTask;
            lock (_sendLock) {
                if (_activeSendTask is { IsCompleted: false } activeSendTask)
                    sendTask = _queuedSendTask ??= QueuedSend(activeSendTask);
                else
                    sendTask = _activeSendTask = Send();
            }
            return sendTask.WaitAsync(cancellationToken);
        }

        // Private methods

        private async Task QueuedSend(Task activeSendTask)
        {
            await activeSendTask.SilentAwait(false);
            // The yield forces an asynchronous continuation, which can't enter the promotion block
            // below before NotifyChanged (still holding _sendLock) assigns _queuedSendTask = this task
            await Task.Yield();
            lock (_sendLock) {
                _activeSendTask = _queuedSendTask;
                _queuedSendTask = null;
            }
            await Send().ConfigureAwait(false);
        }

        private async Task Send()
        {
            // Never runs concurrently with itself: NotifyChanged starts it only when no send is
            // in flight, and QueuedSend only after the active send completes
            try {
                if (StopToken.IsCancellationRequested)
                    return;

                DbContext ??= await DbHub.ContextFactory
                    .CreateDbContextAsync(Shard, StopToken)
                    .ConfigureAwait(false);
                await DbContext.Database
                    .ExecuteSqlRawAsync(NotifySql, StopToken)
                    .ConfigureAwait(false);
            }
            catch (Exception e) {
                if (DbContext is { } dbContext) {
                    DbContext = null;
                    await dbContext.DisposeAsync().ConfigureAwait(false);
                }
                if (!e.IsCancellationOf(StopToken))
                    Owner.Log.LogError(e, "NotifyChanged failed for shard '{Shard}'", Shard);
                throw;
            }
        }
    }
}

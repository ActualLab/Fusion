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
        private readonly Lock _lock = new();
        private Task? _activeNotifyTask;
        private Task? _queuedNotifyTask;

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
            Task? lastNotifyTask;
            lock (_lock)
                lastNotifyTask = _queuedNotifyTask ?? _activeNotifyTask;
            if (lastNotifyTask is not null)
                await lastNotifyTask.SilentAwait(false);
            if (DbContext is { } dbContext) {
                DbContext = null;
                await dbContext.DisposeAsync().ConfigureAwait(false);
            }
        }

        public override Task NotifyChanged(CancellationToken cancellationToken)
        {
            // Coalescing instead of a send queue: at most one NOTIFY is in flight and at most one
            // more is queued behind it. The payload is constant (host id), so the queued one covers
            // every request accepted while it awaits its turn - they all share its task.
            Task notifyTask;
            lock (_lock) {
                // The queued task must be checked first: the active one may be already completed
                // while the queued one hasn't promoted itself yet, and starting a new NOTIFY
                // in this state would bypass the queued one
                if (_queuedNotifyTask is { } queuedNotifyTask)
                    notifyTask = queuedNotifyTask;
                else if (_activeNotifyTask is { IsCompleted: false } activeNotifyTask)
                    notifyTask = _queuedNotifyTask = QueuedNotify(activeNotifyTask);
                else
                    notifyTask = _activeNotifyTask = Notify();
            }
            return notifyTask.WaitAsync(cancellationToken);
        }

        // Private methods

        private async Task QueuedNotify(Task activeNotifyTask)
        {
            await activeNotifyTask.SilentAwait(false);
            // The yield forces an asynchronous continuation, which can't enter the promotion block
            // below before NotifyChanged (still holding _lock) assigns _queuedNotifyTask = this task
            await Task.Yield();
            lock (_lock) {
                if (!ReferenceEquals(_activeNotifyTask, activeNotifyTask)) {
                    // Must be unreachable; skipping the NOTIFY is the recovery here - the send
                    // pipeline stays consistent, and the readers fall back to their check periods
                    Owner.Log.LogCritical(
                        $"{nameof(QueuedNotify)}: the notify task it chained itself behind isn't the active one");
                    _queuedNotifyTask = null;
                    return;
                }

                _activeNotifyTask = _queuedNotifyTask;
                _queuedNotifyTask = null;
            }
            await Notify().ConfigureAwait(false);
        }

        private async Task Notify()
        {
            // Never runs concurrently with itself: NotifyChanged starts it only when no NOTIFY is
            // in flight, and QueuedNotify only after the active one completes
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

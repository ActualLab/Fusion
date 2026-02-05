using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Locking;
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

    // Nested types

    /// <summary>
    /// A <see cref="DbShardWatcher"/> that listens for PostgreSQL notifications
    /// and sends NOTIFY commands to signal log changes.
    /// </summary>
    protected class ShardWatcher : DbShardWatcher
    {
        public NpgsqlDbLogWatcher<TDbContext, TDbEntry> Owner { get; }
        public DbHub<TDbContext> DbHub => Owner.DbHub;
        public AsyncLock NotifyLock { get; } = new();
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
            QuotedNotifyPayload = hostId.Id
#if NETSTANDARD2_0
                .Replace("'", "''");
#else
                .Replace("'", "''", StringComparison.Ordinal);
#endif
            ListenSql = $"LISTEN {channelName}";
            NotifySql = $"NOTIFY {channelName}, '{QuotedNotifyPayload}'";

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
            using var releaser = await NotifyLock.Lock().ConfigureAwait(false);
            if (DbContext is { } dbContext) {
                DbContext = null;
                await dbContext.DisposeAsync().ConfigureAwait(false);
            }
        }

        public override async Task NotifyChanged(CancellationToken cancellationToken)
        {
            var releaser = await NotifyLock.Lock(cancellationToken).ConfigureAwait(false);
            try {
                if (StopToken.IsCancellationRequested)
                    return;

                DbContext ??= await DbHub.ContextFactory
                    .CreateDbContextAsync(Shard, cancellationToken)
                    .ConfigureAwait(false);
                if (StopToken.IsCancellationRequested)
                    return;

                await DbContext.Database
                    .ExecuteSqlRawAsync(NotifySql, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                if (DbContext is { } dbContext) {
                    DbContext = null;
                    await dbContext.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally {
                releaser.Dispose();
            }
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Npgsql;
using ActualLab.Fusion.EntityFramework.Operations;

namespace ActualLab.Fusion.EntityFramework.Npgsql.Operations;

#pragma warning disable EF1002

public class NpgsqlDbOperationLogChangeTracker<TDbContext>(
    NpgsqlDbOperationLogChangeTrackingOptions<TDbContext> options,
    IServiceProvider services
    ) : DbOperationCompletionTrackerBase<TDbContext, NpgsqlDbOperationLogChangeTrackingOptions<TDbContext>>(options, services)
    where TDbContext : DbContext
{
    protected override DbShardWatcher CreateShardWatcher(DbShard shard)
        => new ShardWatcher(this, shard);

    // Nested types

    protected class ShardWatcher : DbShardWatcher
    {
        public ShardWatcher(NpgsqlDbOperationLogChangeTracker<TDbContext> owner, DbShard shard) : base(shard)
        {
            var dbHub = owner.Services.DbHub<TDbContext>();
            var hostId = owner.Services.GetRequiredService<HostId>();

            var watchChain = new AsyncChain($"Watch({shard})", async cancellationToken => {
                var dbContext = await dbHub.CreateDbContext(Shard, cancellationToken).ConfigureAwait(false);
                await using var _ = dbContext.ConfigureAwait(false);

                var database = dbContext.Database;
                await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
                var dbConnection = (NpgsqlConnection) database.GetDbConnection()!;
                dbConnection.Notification += (_, eventArgs) => {
                    if (eventArgs.Payload != hostId.Id)
                        CompleteWaitForChanges();
                };
                await dbContext.Database
                    .ExecuteSqlRawAsync($"LISTEN {owner.Options.ChannelName}", cancellationToken)
                    .ConfigureAwait(false);
                while (!cancellationToken.IsCancellationRequested)
                    await dbConnection.WaitAsync(cancellationToken).ConfigureAwait(false);
            }).RetryForever(owner.Options.TrackerRetryDelays, owner.Log);

            _ = watchChain.RunIsolated(StopToken);
        }
    }
}

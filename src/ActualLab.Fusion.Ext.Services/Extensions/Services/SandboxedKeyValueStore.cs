using System.Globalization;
using ActualLab.Fusion.Authentication;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Extensions.Internal;

namespace ActualLab.Fusion.Extensions.Services;

public partial class SandboxedKeyValueStore<TContext>(
        SandboxedKeyValueStore<TContext>.Options settings,
        IServiceProvider services
        ) : ISandboxedKeyValueStore
{
    public record Options
    {
        public static Options Default { get; set; } = new();

        public string SessionKeyPrefixFormat { get; set; } = "@session/{0}";
        public TimeSpan? SessionKeyExpirationTime { get; set; } = TimeSpan.FromDays(30);
        public string UserKeyPrefixFormat { get; set; } = "@user/{0}";
        public TimeSpan? UserKeyExpirationTime { get; set; } = null;
        public IMomentClock? Clock { get; set; } = null;
    }

    protected Options Settings { get; } = settings;
    protected IKeyValueStore Store { get; } = services.GetRequiredService<IKeyValueStore>();
    protected IAuth Auth { get; } = services.GetRequiredService<IAuth>();
    protected IDbShardResolver ShardResolver { get; } = services.GetRequiredService<IDbShardResolver>();
    protected IMomentClock Clock { get; } = settings.Clock ?? services.Clocks().SystemClock;

    // Commands

    public virtual async Task Set(SandboxedKeyValueStore_Set command, CancellationToken cancellationToken = default)
    {
        if (InvalidationMode.IsOn) return;

        var keyChecker = await GetKeyChecker(command.Session, cancellationToken).ConfigureAwait(false);
        var items = command.Items;
        var newItems = new (string Key, string Value, Moment? ExpiresAt)[items.Length];
        for (var i = 0; i < items.Length; i++) {
            var item = items[i];
            var expiresAt = item.ExpiresAt;
            keyChecker.CheckKey(item.Key, ref expiresAt);
            newItems[i] = (item.Key, item.Value, expiresAt);
        }

        var shard = ShardResolver.Resolve<TContext>(command);
        await Store.Set(shard, newItems, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task Remove(SandboxedKeyValueStore_Remove command, CancellationToken cancellationToken = default)
    {
        if (InvalidationMode.IsOn) return;

        var keyChecker = await GetKeyChecker(command.Session, cancellationToken).ConfigureAwait(false);
        var keys = command.Keys;
        foreach (var t in keys)
            keyChecker.CheckKey(t);

        var shard = ShardResolver.Resolve<TContext>(command);
        await Store.Remove(shard, keys, cancellationToken).ConfigureAwait(false);
    }

    // Compute methods

    public virtual async Task<string?> Get(Session session, string key, CancellationToken cancellationToken = default)
    {
        var keyChecker = await GetKeyChecker(session, cancellationToken).ConfigureAwait(false);
        keyChecker.CheckKey(key);

        var shard = ShardResolver.Resolve<TContext>(session);
        return await Store.Get(shard, key, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<int> Count(Session session, string prefix, CancellationToken cancellationToken = default)
    {
        var keyChecker = await GetKeyChecker(session, cancellationToken).ConfigureAwait(false);
        keyChecker.CheckKeyPrefix(prefix);

        var shard = ShardResolver.Resolve<TContext>(session);
        return await Store.Count(shard, prefix, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<string[]> ListKeySuffixes(
        Session session, string prefix, PageRef<string> pageRef,
        SortDirection sortDirection = SortDirection.Ascending, CancellationToken cancellationToken = default)
    {
        var keyChecker = await GetKeyChecker(session, cancellationToken).ConfigureAwait(false);
        keyChecker.CheckKeyPrefix(prefix);

        var shard = ShardResolver.Resolve<TContext>(session);
        return await Store.ListKeySuffixes(shard, prefix, pageRef, sortDirection, cancellationToken).ConfigureAwait(false);
    }

    [ComputeMethod]
    protected virtual async Task<KeyChecker> GetKeyChecker(
        Session session, CancellationToken cancellationToken = default)
    {
        if (session == null!)
            throw Errors.KeyViolatesSandboxedKeyValueStoreConstraints();

        var user = await Auth.GetUser(session, cancellationToken).ConfigureAwait(false);
        var keyChecker = new KeyChecker() {
            Clock = Clock,
            Prefix = string.Format(CultureInfo.InvariantCulture, Settings.SessionKeyPrefixFormat, session.Id),
            ExpirationTime = Settings.SessionKeyExpirationTime,
        };
        if (user != null)
            keyChecker = keyChecker with {
                SecondaryPrefix = string.Format(CultureInfo.InvariantCulture, Settings.UserKeyPrefixFormat, user.Id),
                SecondaryExpirationTime = Settings.UserKeyExpirationTime,
            };
        return keyChecker;
    }
}

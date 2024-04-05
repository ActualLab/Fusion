using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.EntityFramework;

namespace ActualLab.Fusion.Extensions;

public static class KeyValueStoreExt
{
    public static ListFormat ListFormat { get; set; } = ListFormat.SlashSeparated;
    public static char Delimiter => ListFormat.Delimiter;

    // Set

    public static Task Set<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]T>(
        this IKeyValueStore keyValueStore,
        DbShard shard, string key, T value, CancellationToken cancellationToken = default)
        => keyValueStore.Set(shard, key, value, null, cancellationToken);

    public static Task Set<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
        (this IKeyValueStore keyValueStore,
        DbShard shard, string key, T value, Moment? expiresAt, CancellationToken cancellationToken = default)
    {
#pragma warning disable IL2026
        var sValue = NewtonsoftJsonSerialized.New(value).Data;
#pragma warning restore IL2026
        return keyValueStore.Set(shard, key, sValue, expiresAt, cancellationToken);
    }

    public static Task Set(this IKeyValueStore keyValueStore,
        DbShard shard, string key, string value, CancellationToken cancellationToken = default)
        => keyValueStore.Set(shard, key, value, null, cancellationToken);

    public static Task Set(this IKeyValueStore keyValueStore,
        DbShard shard, string key, string value, Moment? expiresAt, CancellationToken cancellationToken = default)
    {
        var command = new KeyValueStore_Set(shard, [(key, value, expiresAt)]);
        return keyValueStore.GetCommander().Call(command, cancellationToken);
    }

    public static Task Set(this IKeyValueStore keyValueStore,
        DbShard shard,
        (string Key, string Value, Moment? ExpiresAt)[] items,
        CancellationToken cancellationToken = default)
    {
        var command = new KeyValueStore_Set(shard, items);
        return keyValueStore.GetCommander().Call(command, cancellationToken);
    }

    // Remove

    public static Task Remove(this IKeyValueStore keyValueStore,
        DbShard shard, string key, CancellationToken cancellationToken = default)
    {
        var command = new KeyValueStore_Remove(shard, [key]);
        return keyValueStore.GetCommander().Call(command, cancellationToken);
    }

    public static Task Remove(this IKeyValueStore keyValueStore,
        DbShard shard, string[] keys, CancellationToken cancellationToken = default)
    {
        var command = new KeyValueStore_Remove(shard, keys);
        return keyValueStore.GetCommander().Call(command, cancellationToken);
    }

    // TryGet & Get

    public static async ValueTask<Option<T>> TryGet<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
        (this IKeyValueStore keyValueStore, DbShard shard, string key, CancellationToken cancellationToken = default)
    {
        var sValue = await keyValueStore.Get(shard, key, cancellationToken).ConfigureAwait(false);
#pragma warning disable IL2026
        return sValue == null ? Option<T>.None : NewtonsoftJsonSerialized.New<T>(sValue).Value;
#pragma warning restore IL2026
    }

    public static async ValueTask<T?> Get<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
        (this IKeyValueStore keyValueStore, DbShard shard, string key, CancellationToken cancellationToken = default)
    {
        var sValue = await keyValueStore.Get(shard, key, cancellationToken).ConfigureAwait(false);
#pragma warning disable IL2026
        return sValue == null ? default : NewtonsoftJsonSerialized.New<T>(sValue).Value;
#pragma warning restore IL2026
    }

    // ListKeysByPrefix

    public static Task<string[]> ListKeySuffixes(this IKeyValueStore keyValueStore,
        DbShard shard,
        string prefix,
        PageRef<string> pageRef,
        CancellationToken cancellationToken = default)
        => keyValueStore.ListKeySuffixes(shard, prefix, pageRef, SortDirection.Ascending, cancellationToken);
}

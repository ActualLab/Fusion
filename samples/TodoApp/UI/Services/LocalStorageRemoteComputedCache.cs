using ActualLab.Fusion.Client.Caching;
using ActualLab.IO;
using ActualLab.Rpc.Caching;
using Blazored.LocalStorage;
using Cysharp.Text;
using MemoryPack;

namespace Samples.TodoApp.UI.Services;

public sealed class LocalStorageRemoteComputedCache : RemoteComputedCache
{
    public new record Options : RemoteComputedCache.Options
    {
        public static new readonly Options Default = new() { Version = "1.0" };

        public string KeyPrefix { get; init; } = "";
    }

    private readonly ISyncLocalStorageService _storage;
    private readonly string _keyPrefix;

    // ReSharper disable once ConvertToPrimaryConstructor
    public LocalStorageRemoteComputedCache(Options settings, IServiceProvider services, bool initialize = true)
        : base(settings, services, initialize)
    {
        _keyPrefix = settings.KeyPrefix;
        _storage = services.GetRequiredService<ISyncLocalStorageService>();
    }

    public override ValueTask<RpcCacheValue> Get(RpcCacheKey key, CancellationToken cancellationToken = default)
    {
        var sValue = _storage.GetItemAsString(GetStringKey(key));
        if (sValue.IsNullOrEmpty())
            return default;

        var bytes  = Convert.FromBase64String(sValue);
        var value = MemoryPackSerializer.Deserialize<RpcCacheValue>(bytes);
        return new ValueTask<RpcCacheValue>(value);
    }

    public override void Set(RpcCacheKey key, RpcCacheValue value)
    {
        using var buffer = new ArrayPoolBuffer<byte>();
        MemoryPackSerializer.Serialize(buffer, value);
        var sValue = Convert.ToBase64String(buffer.WrittenSpan);
        _storage.SetItemAsString(GetStringKey(key), sValue);
    }

    public override void Remove(RpcCacheKey key)
        => _storage.RemoveItem(GetStringKey(key));

    public override Task Clear(CancellationToken cancellationToken = default)
    {
        _storage.Clear();
        return Task.CompletedTask;
    }

    private string GetStringKey(RpcCacheKey key)
    {
#if false // The simplest impl., which doesn't produce "readable" keys
        using var buffer = new ArrayPoolBuffer<byte>();
        MemoryPackSerializer.Serialize(buffer, key);
        return Convert.ToBase64String(buffer.WrittenSpan);
#else
        // And that's one of ways to produce a readable one
        var data = Convert.ToBase64String(key.ArgumentData.Span);
        return ZString.Concat(_keyPrefix, key.Name, ' ', data);
#endif
    }
}

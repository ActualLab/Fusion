using ActualLab.Fusion.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Fusion.Client.Caching;

public abstract partial class RemoteComputedCache : RpcServiceBase, IRemoteComputedCache
{
    public static RpcCacheKey VersionKey { get; set; } = new("Version", default);

    public record Options(string Version = "")
    {
        public static Options Default { get; set; } = new();

        public LogLevel LogLevel { get; init; } = LogLevel.Debug;
    }

    protected RpcArgumentSerializer ArgumentSerializer;
    protected RpcMethodResolver AnyMethodResolver;
    protected ILogger? DefaultLog;

    public Options Settings { get; }
    public Task WhenInitialized { get; protected set; } = Task.CompletedTask;

    protected RemoteComputedCache(Options settings, IServiceProvider services, bool initialize = true)
        : base(services)
    {
        Settings = settings;
        DefaultLog = Log.IfEnabled(Settings.LogLevel);
        ArgumentSerializer = Hub.SerializationFormats.GetDefault(false).ArgumentSerializer;
        AnyMethodResolver = Hub.ServiceRegistry.AnyMethodResolver;
        if (initialize)
            // ReSharper disable once VirtualMemberCallInConstructor
#pragma warning disable MA0040, CA2214
            WhenInitialized = Initialize(settings.Version);
#pragma warning restore MA0040, CA2214
    }

    public virtual async Task Initialize(string version, CancellationToken cancellationToken = default)
    {
        if (version.IsNullOrEmpty()) {
            DefaultLog?.Log(Settings.LogLevel, "Initialize: no version -> will reuse cache content");
            return;
        }

        var expectedData = EncodingExt.Utf8NoBom.GetBytes(version);
        var entry = await Get(VersionKey, cancellationToken).ConfigureAwait(false);
        if (entry is not null && entry.Data.Span.SequenceEqual(expectedData)) {
            DefaultLog?.Log(Settings.LogLevel, "Initialize: version match -> will reuse cache content");
            return;
        }

        DefaultLog?.Log(Settings.LogLevel, "Initialize: version mismatch -> will clear cache content");
        await Clear(cancellationToken).ConfigureAwait(false);
        Set(VersionKey, new RpcCacheValue(expectedData, ""));
    }

    public async ValueTask<RpcCacheEntry?> Get(ComputeMethodInput input, RpcCacheKey key, CancellationToken cancellationToken)
    {
        var methodDef = AnyMethodResolver[key.Name];
        if (methodDef is null)
            return null;

        try {
            if (!WhenInitialized.IsCompleted)
                await WhenInitializedUnlessVersionKey(key).WaitAsync(cancellationToken).SilentAwait(false);

            var entry = await Get(key, cancellationToken).ConfigureAwait(false);
            if (entry is null) {
                DefaultLog?.Log(Settings.LogLevel, "[?] {Key} -> miss", key);
                return null;
            }

            DefaultLog?.Log(Settings.LogLevel, "[?] {Key} -> hit", key);
            var resultList = methodDef.ResultListType.Factory.Invoke();
            ArgumentSerializer.Deserialize(ref resultList, methodDef.HasPolymorphicResult, entry.Data);
            return new RpcCacheEntry(key, entry, resultList.Get0Untyped());
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            Log.LogWarning("Read failed for key `{Key}`: {Type}({Message})",
                key, e.GetType().GetName(), e.Message);
            return null;
        }
    }

    public abstract ValueTask<RpcCacheValue?> Get(RpcCacheKey key, CancellationToken cancellationToken = default);
    public abstract void Set(RpcCacheKey key, RpcCacheValue value);
    public abstract void Remove(RpcCacheKey key);
    public abstract Task Clear(CancellationToken cancellationToken = default);

    // Protected methods

    protected Task WhenInitializedUnlessVersionKey(RpcCacheKey key) =>
        WhenInitialized.IsCompleted || ReferenceEquals(key, VersionKey)
            ? Task.CompletedTask
            : WhenInitialized;
}

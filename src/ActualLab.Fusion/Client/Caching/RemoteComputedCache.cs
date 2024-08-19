using System.Diagnostics.CodeAnalysis;
using System.Text;
using ActualLab.Fusion.Interception;
using ActualLab.Internal;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Fusion.Client.Caching;

public abstract partial class RemoteComputedCache : RpcServiceBase, IRemoteComputedCache
{
    public static RpcCacheKey VersionKey { get; set; } = new("", "Version", TextOrBytes.EmptyBytes);

    public record Options(string Version = "")
    {
        public static Options Default { get; set; } = new();

        public LogLevel LogLevel { get; init; } = LogLevel.Debug;
    }

    protected RpcArgumentSerializer ArgumentSerializer;
    protected ILogger? DefaultLog;

    public Options Settings { get; }
    public Task WhenInitialized { get; protected set; } = Task.CompletedTask;

    protected RemoteComputedCache(Options settings, IServiceProvider services, bool initialize = true)
        : base(services)
    {
        Settings = settings;
        DefaultLog = Log.IfEnabled(Settings.LogLevel);
        ArgumentSerializer = Hub.InternalServices.ArgumentSerializer;
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

        var expectedData = new TextOrBytes(Encoding.UTF8.GetBytes(version));
        var entry = await Get(VersionKey, cancellationToken).ConfigureAwait(false);
        if (!entry.IsNone) {
            if (entry.Data.DataEquals(expectedData)) {
                DefaultLog?.Log(Settings.LogLevel, "Initialize: version match -> will reuse cache content");
                return;
            }
        }

        DefaultLog?.Log(Settings.LogLevel, "Initialize: version mismatch -> will clear cache content");
        await Clear(cancellationToken).ConfigureAwait(false);
        Set(VersionKey, new RpcCacheValue(expectedData, ""));
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public async ValueTask<RpcCacheEntry<T>?> Get<T>(
        ComputeMethodInput input, RpcCacheKey key, CancellationToken cancellationToken)
    {
        var serviceDef = Hub.ServiceRegistry.Get(key.Service);
        var methodDef = serviceDef?.GetMethod(key.Method);
        if (methodDef == null)
            return null;

        try {
            if (!WhenInitialized.IsCompleted)
                await WhenInitializedUnlessVersionKey(key).WaitAsync(cancellationToken).SilentAwait(false);

            var entry = await Get(key, cancellationToken).ConfigureAwait(false);
            if (entry.IsNone) {
                DefaultLog?.Log(Settings.LogLevel, "[?] {Key} -> miss", key);
                return null;
            }

            DefaultLog?.Log(Settings.LogLevel, "[?] {Key} -> hit", key);
            var resultList = methodDef.ResultListType.Factory.Invoke();
            ArgumentSerializer.Deserialize(ref resultList, methodDef.AllowResultPolymorphism, entry.Data);
            return new RpcCacheEntry<T>(key, entry, resultList.Get0<T>());
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            Log.LogError(e, "Cached result read failed");
            return null;
        }
    }

    public abstract ValueTask<RpcCacheValue> Get(RpcCacheKey key, CancellationToken cancellationToken = default);
    public abstract void Set(RpcCacheKey key, RpcCacheValue value);
    public abstract void Remove(RpcCacheKey key);
    public abstract Task Clear(CancellationToken cancellationToken = default);

    // Protected methods

    protected Task WhenInitializedUnlessVersionKey(RpcCacheKey key) =>
        WhenInitialized.IsCompleted || ReferenceEquals(key, VersionKey)
            ? Task.CompletedTask
            : WhenInitialized;
}

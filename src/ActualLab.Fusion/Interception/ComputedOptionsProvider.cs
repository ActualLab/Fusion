using ActualLab.Fusion.Client.Caching;

namespace ActualLab.Fusion.Interception;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class ComputedOptionsProvider(IServiceProvider services)
{
    private readonly LazySlim<IRemoteComputedCache?> _remoteComputedCacheLazy
        = new(services.GetService<IRemoteComputedCache>);

    protected IServiceProvider Services { get; } = services;
    protected IRemoteComputedCache? RemoteComputedCache => _remoteComputedCacheLazy.Value;

    public virtual ComputedOptions? GetComputedOptions(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        MethodInfo method)
    {
        var options = ComputedOptions.Get(type, method);
        if (options is null || options.RemoteComputedCacheMode == RemoteComputedCacheMode.NoCache)
            return options;

        if (RemoteComputedCache is null)
            options = options with { RemoteComputedCacheMode = RemoteComputedCacheMode.NoCache };
        return options;
    }
}

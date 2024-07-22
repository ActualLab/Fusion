using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Client.Caching;

namespace ActualLab.Fusion.Interception;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class ComputedOptionsProvider(IServiceProvider services)
{
    protected readonly bool HasRemoteComputedCache =
        services.GetService<IRemoteComputedCache>() != null;

    public virtual ComputedOptions? GetComputedOptions(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        MethodInfo method)
    {
        var options = ComputedOptions.Get(type, method);
        if (options == null || options.RemoteComputedCacheMode == RemoteComputedCacheMode.NoCache)
            return options;

        if (!HasRemoteComputedCache)
            options = options with { RemoteComputedCacheMode = RemoteComputedCacheMode.NoCache };
        return options;
    }
}

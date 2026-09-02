using Microsoft.Extensions.Hosting;

namespace ActualLab.DependencyInjection;

public static class HostApplicationLifetimeExt
{
    public static bool IsApplicationStopping(this IHostApplicationLifetime? hostLifetime)
        => hostLifetime?.ApplicationStopping.IsCancellationRequested == true;
}

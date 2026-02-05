namespace ActualLab.Versioning;

/// <summary>
/// Extension methods for <see cref="IServiceProvider"/> to resolve versioning services.
/// </summary>
public static class ServiceProviderExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VersionGenerator<TVersion> VersionGenerator<TVersion>(this IServiceProvider services)
        where TVersion : notnull
        => services.GetRequiredService<VersionGenerator<TVersion>>();
}

namespace ActualLab.Versioning;

public static class ServiceProviderExt
{
    extension(IServiceProvider services)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VersionGenerator<TVersion> VersionGenerator<TVersion>()
            where TVersion : notnull
            => services.GetRequiredService<VersionGenerator<TVersion>>();
    }
}

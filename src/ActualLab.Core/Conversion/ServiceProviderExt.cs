namespace ActualLab.Conversion;

/// <summary>
/// Extension methods for <see cref="IServiceProvider"/> to access converters.
/// </summary>
public static class ServiceProviderExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IConverterProvider Converters(this IServiceProvider services)
        => services.GetService<IConverterProvider>() ?? ConverterProvider.Default;
}

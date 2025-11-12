namespace ActualLab.Conversion;

public static class ServiceProviderExt
{
    extension(IServiceProvider services)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IConverterProvider Converters()
            => services.GetService<IConverterProvider>() ?? ConverterProvider.Default;
    }
}

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.Conversion.Internal;

namespace ActualLab.Conversion;

public static class ServiceCollectionExt
{
    public static IServiceCollection AddConverters(
        this IServiceCollection services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type? sourceConverterProviderGenericType = null)
    {
        sourceConverterProviderGenericType ??= typeof(DefaultSourceConverterProvider<>);
        services.TryAddSingleton<IConverterProvider, DefaultConverterProvider>();
        services.TryAddSingleton(typeof(ISourceConverterProvider<>), sourceConverterProviderGenericType);
        return services;
    }
}

using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Extensions.Internal;
using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion.Extensions;

public static class FusionBuilderExt
{
    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static FusionBuilder AddFusionTime(this FusionBuilder fusion,
        Func<IServiceProvider, FusionTime.Options>? optionsFactory = null)
    {
        var services = fusion.Services;
        services.AddSingleton(optionsFactory, _ => FusionTime.Options.Default);
        if (!services.HasService<IFusionTime>())
            fusion.AddService<IFusionTime, FusionTime>();
        return fusion;
    }
}

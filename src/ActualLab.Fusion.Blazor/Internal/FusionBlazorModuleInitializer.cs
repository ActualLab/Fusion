using ActualLab.Trimming;

namespace ActualLab.Fusion.Blazor.Internal;

#pragma warning disable CA2255

/// <summary>
/// Module initializer that retains the built-in <see cref="ParameterComparer"/> types,
/// which <see cref="ParameterComparerProvider"/> instantiates reflectively.
/// </summary>
internal static class FusionBlazorModuleInitializer
{
    static FusionBlazorModuleInitializer()
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        // These are named only via typeof() - in [ParameterComparer(...)] or KnownComparerTypes -
        // so trimming keeps the types but drops the constructors CreateInstance needs.
        // The generic comparers are closed by user types, so their usage sites must root them.
        CodeKeeper.Keep<DefaultParameterComparer>();
        CodeKeeper.Keep<ByValueParameterComparer>();
        CodeKeeper.Keep<ByRefParameterComparer>();
        CodeKeeper.Keep<ByNoneParameterComparer>();
        CodeKeeper.Keep<ByUuidParameterComparer>();
    }

#if NET8_0_OR_GREATER
    [ModuleInitializer]
#endif
    internal static void Touch()
    { }
}

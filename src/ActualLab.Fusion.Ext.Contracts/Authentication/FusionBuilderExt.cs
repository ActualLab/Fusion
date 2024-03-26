using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion.Authentication;

public static class FusionBuilderExt
{
    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static FusionBuilder AddAuthClient(this FusionBuilder fusion)
        => fusion.AddClient<IAuth>();
}

using Microsoft.AspNetCore.Mvc.Controllers;

namespace ActualLab.Fusion.Server.Internal;

public sealed class ControllerFilter(Func<TypeInfo, bool> filter) : ControllerFeatureProvider
{
    private Func<TypeInfo, bool> Filter { get; } = filter;

    protected override bool IsController(TypeInfo typeInfo)
        => base.IsController(typeInfo) && Filter.Invoke(typeInfo);
}

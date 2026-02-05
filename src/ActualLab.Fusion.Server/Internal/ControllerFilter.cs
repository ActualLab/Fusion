using Microsoft.AspNetCore.Mvc.Controllers;

namespace ActualLab.Fusion.Server.Internal;

/// <summary>
/// A <see cref="ControllerFeatureProvider"/> that applies a custom filter predicate
/// to determine which types qualify as controllers.
/// </summary>
public sealed class ControllerFilter(Func<TypeInfo, bool> filter) : ControllerFeatureProvider
{
    private Func<TypeInfo, bool> Filter { get; } = filter;

    protected override bool IsController(TypeInfo typeInfo)
        => base.IsController(typeInfo) && Filter.Invoke(typeInfo);
}

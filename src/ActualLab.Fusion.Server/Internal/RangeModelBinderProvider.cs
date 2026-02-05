using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ActualLab.Fusion.Server.Internal;

/// <summary>
/// An <see cref="IModelBinderProvider"/> that supplies <see cref="RangeModelBinder"/>
/// for <see cref="Range{T}"/> model types.
/// </summary>
public class RangeModelBinderProvider  : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        var modelType = context.Metadata.ModelType;
        if (modelType.IsConstructedGenericType && modelType.GetGenericTypeDefinition() == typeof(Range<>))
            return new RangeModelBinder();

        return null;
    }
}

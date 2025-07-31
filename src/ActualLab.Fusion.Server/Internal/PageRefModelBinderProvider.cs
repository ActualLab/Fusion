using Microsoft.AspNetCore.Mvc.ModelBinding;
using ActualLab.Fusion.Extensions;

namespace ActualLab.Fusion.Server.Internal;

public class PageRefModelBinderProvider  : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        var modelType = context.Metadata.ModelType;
        if (modelType.IsConstructedGenericType && modelType.GetGenericTypeDefinition() == typeof(PageRef<>))
            return new PageRefModelBinder();

        return null;
    }
}

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ActualLab.Fusion.Server.Internal;

public class SimpleModelBinderProvider<TModel, TBinder>  : IModelBinderProvider
    where TBinder : class, IModelBinder, new()
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var modelType = context.Metadata.ModelType;
        if (modelType == typeof(TModel))
            return new TBinder();

        return null;
    }
}

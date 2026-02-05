using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ActualLab.Fusion.Server.Internal;

/// <summary>
/// A generic <see cref="IModelBinderProvider"/> that returns an instance of
/// <typeparamref name="TBinder"/> when the model type matches <typeparamref name="TModel"/>.
/// </summary>
public class SimpleModelBinderProvider<TModel, TBinder>  : IModelBinderProvider
    where TBinder : class, IModelBinder, new()
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        var modelType = context.Metadata.ModelType;
        if (modelType == typeof(TModel))
            return new TBinder();

        return null;
    }
}

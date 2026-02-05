using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ActualLab.Fusion.Server.Internal;

/// <summary>
/// An <see cref="IModelBinder"/> that binds <see cref="Moment"/> values from request data.
/// </summary>
public class MomentModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext is null)
            throw new ArgumentNullException(nameof(bindingContext));

        try {
            var sValue = bindingContext.ValueProvider.GetValue(bindingContext.ModelName).FirstValue ?? "";
            var result = Moment.Parse(sValue);
            bindingContext.Result = ModelBindingResult.Success(result);
        }
        catch (Exception) {
            bindingContext.Result = ModelBindingResult.Failed();
        }
        return Task.CompletedTask;
    }
}

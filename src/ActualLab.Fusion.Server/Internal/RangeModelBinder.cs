using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ActualLab.Fusion.Server.Internal;

public class RangeModelBinder : IModelBinder
{
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume server-side code is fully preserved")]
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext == null)
            throw new ArgumentNullException(nameof(bindingContext));

        try {
            var sValue = bindingContext.ValueProvider.GetValue(bindingContext.ModelName).FirstValue ?? "";
            var result = typeof(Range<>)
                .MakeGenericType(bindingContext.ModelType.GetGenericArguments()[0])
                .GetMethod(nameof(Range<long>.Parse))!
                .Invoke(null, [sValue]);
            bindingContext.Result = ModelBindingResult.Success(result);
        }
        catch (Exception) {
            bindingContext.Result = ModelBindingResult.Failed();
        }
        return Task.CompletedTask;
    }
}

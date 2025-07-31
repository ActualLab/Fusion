using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ActualLab.Fusion.Extensions;

namespace ActualLab.Fusion.Server.Internal;

[UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume server-side code is fully preserved")]
public class PageRefModelBinder : IModelBinder
{
    private static readonly MethodInfo ParseMethod = typeof(PageRef).GetMethod(nameof(PageRef.Parse))!;

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext is null)
            throw new ArgumentNullException(nameof(bindingContext));

        try {
            var sValue = bindingContext.ValueProvider.GetValue(bindingContext.ModelName).FirstValue ?? "";
            var result = ParseMethod
                .MakeGenericMethod(bindingContext.ModelType.GetGenericArguments()[0])
                .Invoke(null, [sValue]);
            bindingContext.Result = ModelBindingResult.Success(result);
        }
        catch (Exception) {
            bindingContext.Result = ModelBindingResult.Failed();
        }
        return Task.CompletedTask;
    }
}

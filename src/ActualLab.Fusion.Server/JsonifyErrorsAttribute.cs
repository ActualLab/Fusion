using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ActualLab.Fusion.Server;

/// <summary>
/// An MVC exception filter that serializes exceptions as JSON error responses.
/// </summary>
public sealed class JsonifyErrorsAttribute : ExceptionFilterAttribute
{
    public override Task OnExceptionAsync(ExceptionContext context)
    {
        var exception = context.Exception;
        var httpContext = context.HttpContext;
        var services = httpContext.RequestServices;

        var log = services.GetRequiredService<ILogger<JsonifyErrorsAttribute>>();
        log.LogError(exception, "Error message: {Message}", exception.Message);

        var serializer = TypeDecoratingTextSerializer.Default;
        var content = serializer.Write(exception.ToExceptionInfo());
        var result = new ContentResult() {
            Content = content,
            ContentType = "application/json",
            StatusCode = (int)HttpStatusCode.InternalServerError,
        };
        context.ExceptionHandled = true;
        return result.ExecuteResultAsync(context);
    }
}

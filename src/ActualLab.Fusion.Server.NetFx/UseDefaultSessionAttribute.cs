using System.Net.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using ActualLab.Fusion.Authentication;

namespace ActualLab.Fusion.Server;

public sealed class UseDefaultSessionAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(HttpActionContext actionContext)
    {
        if (actionContext.Request.Method != HttpMethod.Post)
            return;
        foreach (var (_, argument) in actionContext.ActionArguments) {
            if (argument is ISessionCommand command && command.Session.IsDefault()) {
                var services = actionContext.GetAppServices();
                var sessionResolver = services.GetRequiredService<ISessionResolver>();
                command.UseDefaultSession(sessionResolver);
            }
        }
    }
}

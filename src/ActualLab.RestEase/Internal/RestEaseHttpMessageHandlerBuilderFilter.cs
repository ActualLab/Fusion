using Microsoft.Extensions.Http;

namespace ActualLab.RestEase.Internal;

// ReSharper disable once ClassNeverInstantiated.Global
public class RestEaseHttpMessageHandlerBuilderFilter : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
        => builder => {
            var restEaseHttpMessageHandler = builder.Services.CreateInstance<RestEaseHttpMessageHandler>();
            // Run other builders first
            next(builder);
            builder.AdditionalHandlers.Insert(0, restEaseHttpMessageHandler);
        };
}

using Microsoft.Extensions.Http;

namespace ActualLab.RestEase.Internal;

// ReSharper disable once ClassNeverInstantiated.Global
/// <summary>
/// An <see cref="IHttpMessageHandlerBuilderFilter"/> that inserts
/// <see cref="RestEaseHttpMessageHandler"/> into the HTTP message handler pipeline.
/// </summary>
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

namespace ActualLab.Rpc.Clients.Internal;

/// <summary>
/// Owns the resources backing an <see cref="RpcHttpClient"/> connection and releases them on disposal:
/// completes the request body and disposes the HTTP request/response messages.
/// </summary>
public sealed class RpcHttpConnectionOwner(
    DuplexHttpContent content,
    HttpRequestMessage request,
    HttpResponseMessage response
    ) : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        content.Complete(); // Lets the request body pump (DuplexHttpContent.SerializeToStreamAsync) finish
        request.Dispose();
        response.Dispose();
        return default;
    }
}

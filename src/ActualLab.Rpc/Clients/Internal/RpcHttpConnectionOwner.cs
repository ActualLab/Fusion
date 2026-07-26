namespace ActualLab.Rpc.Clients.Internal;

/// <summary>
/// Owns the resources backing an <see cref="RpcHttpClient"/> connection and releases them on disposal:
/// aborts the underlying HTTP/2 stream, completes the request body, and disposes the
/// HTTP request/response messages.
/// </summary>
public sealed class RpcHttpConnectionOwner(
    DuplexHttpContent content,
    HttpRequestMessage request,
    HttpResponseMessage response,
    CancellationTokenSource requestTokenSource,
    HttpClient httpClient
    ) : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        // Cancelling the token the request was sent with is what resets its HTTP/2 stream.
        // Without it the stream merely half-closes: the server keeps writing a response nobody
        // reads, its flow-control window fills, and since Kestrel multiplexes every stream of a
        // connection onto one output pipe, the next request on that connection can't even flush
        // its response headers - so a reconnecting peer times out forever.
        requestTokenSource.CancelAndDisposeSilently();
        content.Complete(); // Lets the request body pump (DuplexHttpContent.SerializeToStreamAsync) finish
        request.Dispose();
        response.Dispose();
        httpClient.Dispose(); // Owned by this connection - see RpcHttpClient.CreateHttpClient
        return default;
    }
}

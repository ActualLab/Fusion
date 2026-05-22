using System.Net;
using System.Net.Http.Headers;

namespace ActualLab.Rpc.Clients.Internal;

/// <summary>
/// An <see cref="HttpContent"/> that hands its request body <see cref="Stream"/> to the caller and keeps
/// the request open until <see cref="Complete"/> is called, which enables full-duplex HTTP/2 exchange.
/// </summary>
public sealed class DuplexHttpContent : HttpContent
{
    private readonly TaskCompletionSource<Stream> _whenStreamReady = TaskCompletionSourceExt.New<Stream>();
    private readonly TaskCompletionSource _whenCompleted = TaskCompletionSourceExt.New();

    public Task<Stream> WhenStreamReady => _whenStreamReady.Task;

    public DuplexHttpContent()
        => Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

    public void Complete()
        => _whenCompleted.TrySetResult();

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        _whenStreamReady.TrySetResult(stream);
        await _whenCompleted.Task.ConfigureAwait(false);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = -1;
        return false;
    }
}

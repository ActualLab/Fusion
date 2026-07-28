using System.Net;
using System.Net.Http.Headers;
using ActualLab.RestEase.Internal;

namespace ActualLab.Tests.RestEase;

public class RestEaseErrorHandlingTest
{
    [Fact]
    public async Task OversizedErrorMustNotBeBuffered()
    {
        var options = new RestEaseHttpMessageHandler.Options { MaxErrorSize = 1024 };
        var stream = new CountingStream(8 * 1024 * 1024);
        var response = NewResponse(new StreamContent(stream), "application/json");

        var error = await Send(response, options);

        error.Should().BeOfType<RemoteException>();
        error.Message.Should().Contain("1024");
        stream.ReadSize.Should().BeLessThanOrEqualTo(options.MaxErrorSize + 1);
    }

    [Fact]
    public async Task SmallErrorMustRoundTripToTheSameException()
    {
        var exceptionInfo = new ExceptionInfo(new InvalidOperationException("Some error."));
        var json = TypeDecoratingTextSerializer.Default.Write(exceptionInfo);
        var response = NewResponse(new StringContent(json), "application/json");

        var error = await Send(response);

        error.Should().BeOfType<InvalidOperationException>();
        error.Message.Should().Be("Some error.");
    }

    [Fact]
    public async Task SmallNonJsonErrorMustRoundTripToRemoteException()
    {
        var response = NewResponse(new StringContent("Oops."), "text/plain");

        var error = await Send(response);

        error.Should().BeOfType<RemoteException>();
        error.Message.Should().Be("Oops.");
    }

    [Fact]
    public async Task ErrorReadMustHonorTheCancellationToken()
    {
        var response = NewResponse(new StringContent("Oops."), "text/plain");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var error = await Send(response, cancellationToken: cts.Token);

        error.Should().BeAssignableTo<OperationCanceledException>();
    }

    // Private methods

    private static HttpResponseMessage NewResponse(HttpContent content, string mediaType)
    {
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = content };
    }

    private static async Task<Exception> Send(
        HttpResponseMessage response,
        RestEaseHttpMessageHandler.Options? options = null,
        CancellationToken cancellationToken = default)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var handler = new RestEaseHttpMessageHandler(
            options ?? RestEaseHttpMessageHandler.Options.Default,
            services) {
            InnerHandler = new ResponseHandler(response),
        };
        using var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost");
        try {
            await invoker.SendAsync(request, cancellationToken);
        }
        catch (Exception e) {
            return e;
        }

        throw new InvalidOperationException("The 500 response didn't produce an exception.");
    }

    // Nested types

    private sealed class ResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private sealed class CountingStream(long length) : Stream
    {
        public int ReadSize { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => ReadSize; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var readSize = (int)Math.Min(count, length - ReadSize);
            buffer.AsSpan(offset, readSize).Fill((byte)'a');
            ReadSize += readSize;
            return readSize;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

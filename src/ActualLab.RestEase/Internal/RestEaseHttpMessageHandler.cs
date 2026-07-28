using System.Buffers;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ActualLab.RestEase.Internal;

/// <summary>
/// A <see cref="DelegatingHandler"/> that intercepts HTTP 500 responses from
/// <see cref="JsonifyErrorsAttribute"/>-protected endpoints and deserializes
/// the error into a throwable exception.
/// </summary>
public class RestEaseHttpMessageHandler(RestEaseHttpMessageHandler.Options settings, IServiceProvider services)
    : DelegatingHandler, IHasServices
{
    /// <summary>
    /// Configuration options for <see cref="RestEaseHttpMessageHandler"/>.
    /// </summary>
    public record Options
    {
        public static Options Default { get; set; } = new();

        public int MaxErrorSize { get; init; } = 64 * 1024;
        public int MaxErrorJsonDepth { get; init; } = 64;
    }

    public Options Settings { get; } = settings;
    public IServiceProvider Services { get; } = services;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.InternalServerError) {
            try {
                // [JsonifyErrors] responds with this status code
                var error = await DeserializeError(response, cancellationToken).ConfigureAwait(false);
                throw error;
            }
            finally {
                response.Dispose();
            }
        }
        return response;
    }

    protected virtual async Task<Exception> DeserializeError(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await ReadContent(response.Content, cancellationToken).ConfigureAwait(false);
        if (content is null)
            return Errors.ServerSideErrorIsTooLarge(Settings.MaxErrorSize);

        var contentType = response.Content.Headers.ContentType;
        if (!string.Equals(contentType?.MediaType ?? "", "application/json", StringComparison.Ordinal))
            return new RemoteException(content);

        try {
            var serializer = TypeDecoratingTextSerializer.Default;
            return serializer.Read<ExceptionInfo>(content).ToException()
                ?? Errors.UnknownServerSideError();
        }
        catch (Exception) {
            try {
                using var reader = new JsonTextReader(new StringReader(content)) {
                    MaxDepth = Settings.MaxErrorJsonDepth,
                };
                var jError = JObject.Load(reader);
                var message = jError[nameof(Exception.Message)]?.Value<string>();
                return message.IsNullOrEmpty()
                    ? Errors.UnknownServerSideError()
                    : new RemoteException(message!);
            }
            catch (Exception) {
                return Errors.UnknownServerSideError();
            }
        }
    }

    // Private methods

    private async Task<string?> ReadContent(HttpContent content, CancellationToken cancellationToken)
    {
        // Returns null when the body is larger than MaxErrorSize - a hostile server may stream it forever
        var maxSize = Math.Max(0, Settings.MaxErrorSize);
#if NET5_0_OR_GREATER
        var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
        var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
        var buffer = ArrayPool<byte>.Shared.Rent(maxSize + 1);
        try {
            var size = Math.Min(buffer.Length, maxSize + 1);
            var offset = 0;
            while (offset < size) {
#if NETSTANDARD2_0
                var readSize = await stream
                    .ReadAsync(buffer, offset, size - offset, cancellationToken)
                    .ConfigureAwait(false);
#else
                var readSize = await stream
                    .ReadAsync(buffer.AsMemory(offset, size - offset), cancellationToken)
                    .ConfigureAwait(false);
#endif
                if (readSize == 0)
                    return GetEncoding(content).GetString(buffer, 0, offset);

                offset += readSize;
            }
            return null;
        }
        finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static Encoding GetEncoding(HttpContent content)
    {
        var charSet = content.Headers.ContentType?.CharSet;
        if (charSet.IsNullOrEmpty())
            return Encoding.UTF8;

        try {
            return Encoding.GetEncoding(charSet!.Trim('"'));
        }
        catch (ArgumentException) {
            return Encoding.UTF8;
        }
    }
}

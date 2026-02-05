using System.Net;
using Newtonsoft.Json.Linq;

namespace ActualLab.RestEase.Internal;

/// <summary>
/// A <see cref="DelegatingHandler"/> that intercepts HTTP 500 responses from
/// <see cref="JsonifyErrorsAttribute"/>-protected endpoints and deserializes
/// the error into a throwable exception.
/// </summary>
public class RestEaseHttpMessageHandler(IServiceProvider services) : DelegatingHandler, IHasServices
{
    public IServiceProvider Services { get; } = services;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.InternalServerError) {
            // [JsonifyErrors] responds with this status code
            var error = await DeserializeError(response).ConfigureAwait(false);
            throw error;
        }
        return response;
    }

    private static async Task<Exception> DeserializeError(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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
                var jError = JObject.Parse(content);
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
}

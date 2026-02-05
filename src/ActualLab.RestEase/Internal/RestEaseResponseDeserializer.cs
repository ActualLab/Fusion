using System.Net;
using RestEase;

namespace ActualLab.RestEase.Internal;

/// <summary>
/// A RestEase <see cref="ResponseDeserializer"/> that uses <see cref="ITextSerializer"/>
/// to deserialize HTTP response bodies, returning default for HTTP 204 responses.
/// </summary>
public class RestEaseResponseDeserializer : ResponseDeserializer
{
    public ITextSerializer Serializer { get; init; } = SystemJsonSerializer.Default;

    public override T Deserialize<T>(string? content, HttpResponseMessage response, ResponseDeserializerInfo info)
        => response.StatusCode == HttpStatusCode.NoContent
            ? default!
            : Serializer.Read<T>(content!);
}

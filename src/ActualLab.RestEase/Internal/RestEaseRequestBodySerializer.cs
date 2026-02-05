using System.Net.Http.Headers;
using RestEase;

namespace ActualLab.RestEase.Internal;

/// <summary>
/// A RestEase <see cref="RequestBodySerializer"/> that uses <see cref="ITextSerializer"/>
/// to serialize request bodies as JSON content.
/// </summary>
public class RestEaseRequestBodySerializer : RequestBodySerializer
{
    public ITextSerializer Serializer { get; init; } = SystemJsonSerializer.Default;
    public string ContentType { get; init; } = "application/json";

    public override HttpContent? SerializeBody<T>(T body, RequestBodySerializerInfo info)
    {
        if (body is null)
            return null;

        var content = new StringContent(Serializer.Write<T>(body));
        if (content.Headers.ContentType is null)
            content.Headers.ContentType = new MediaTypeHeaderValue(ContentType);
        else
            content.Headers.ContentType.MediaType = ContentType;
        return content;
    }
}

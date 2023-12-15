using System.Diagnostics.CodeAnalysis;
using System.Net;
using RestEase;
using ActualLab.Internal;

namespace ActualLab.RestEase.Internal;

public class RestEaseResponseDeserializer : ResponseDeserializer
{
    public ITextSerializer Serializer { get; init; } = SystemJsonSerializer.Default;

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
#pragma warning disable IL2046
    public override T Deserialize<T>(string? content, HttpResponseMessage response, ResponseDeserializerInfo info)
#pragma warning restore IL2046
        => response.StatusCode == HttpStatusCode.NoContent
            ? default!
            : Serializer.Read<T>(content!);
}

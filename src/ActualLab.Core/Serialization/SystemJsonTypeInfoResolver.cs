#if NET8_0_OR_GREATER // Source-generated System.Text.Json metadata matters for NativeAOT only
using System.Text.Json.Serialization.Metadata;

namespace ActualLab.Serialization;

/// <summary>
/// An <see cref="IJsonTypeInfoResolver"/> that tries the resolvers added to it in order,
/// e.g. source-generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>s.
/// Resolvers can be added at any time, unlike to a <see cref="JsonSerializerOptions"/> instance
/// that was already used.
/// </summary>
public sealed class SystemJsonTypeInfoResolver : IJsonTypeInfoResolver
{
    private ImmutableArray<IJsonTypeInfoResolver> _resolvers = ImmutableArray<IJsonTypeInfoResolver>.Empty;

    public ImmutableArray<IJsonTypeInfoResolver> Resolvers => _resolvers;

    public void Add(IJsonTypeInfoResolver resolver)
        => ImmutableInterlocked.Update(ref _resolvers,
            static (resolvers, resolver) => resolvers.Contains(resolver) ? resolvers : resolvers.Add(resolver),
            resolver);

    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        foreach (var resolver in _resolvers) {
            var typeInfo = resolver.GetTypeInfo(type, options);
            if (typeInfo is not null)
                return typeInfo;
        }
        return null;
    }
}
#endif

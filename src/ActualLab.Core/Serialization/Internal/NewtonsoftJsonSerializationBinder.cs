using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Serialization;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace ActualLab.Serialization.Internal;

public static class NewtonsoftJsonSerializationBinder
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif

    [field: AllowNull, MaybeNull]
    public static ISerializationBinder Default {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            if (field is { } value)
                return value;
            lock (StaticLock)
                return field ??= new JsonSerializer().SerializationBinder;
        }
    }
}

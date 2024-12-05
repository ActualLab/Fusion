using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Serialization;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace ActualLab.Serialization.Internal;

public static class NewtonsoftJsonSerializationBinder
{
    private static readonly Lock StaticLock = LockFactory.Create();

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

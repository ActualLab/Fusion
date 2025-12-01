using Newtonsoft.Json.Serialization;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace ActualLab.Serialization.Internal;

#pragma warning disable IL2026

public static class NewtonsoftJsonSerializationBinder
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static volatile ISerializationBinder? _default;

    public static ISerializationBinder Default {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            if (_default is { } value)
                return value;
            lock (StaticLock)
                // ReSharper disable once NonAtomicCompoundOperator
                return _default ??= new JsonSerializer().SerializationBinder;
        }
    }
}

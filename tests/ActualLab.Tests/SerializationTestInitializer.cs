using ActualLab.Reflection;
using Newtonsoft.Json;

namespace ActualLab.Tests;

#pragma warning disable CA2255

// Registers the format packages these tests cover, so their serializers join the SerializationTestExt matrix;
// their RPC formats are in RpcTestFormats.Catalog
public static class SerializationTestInitializer
{
    private static int _isInitialized;

    [ModuleInitializer]
    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _isInitialized, 1) != 0)
            return;

#if !NETFRAMEWORK
        MemoryPackSerialization.Register();
        // ActualLab.Core.Legacy types (OptionSet, etc.) aren't MemoryPack-serializable
        SerializationTestExt.Register(new TestSerializer("MemoryPack", () => MemoryPackByteSerializer.Default) {
            CanSerialize = static type => !string.Equals(
                type.Assembly.GetName().Name, "ActualLab.Core.Legacy", StringComparison.Ordinal),
        });
#endif
        NewtonsoftJsonSerialization.Register();
        var newtonsoftJsonSettings = MemberwiseCloner.Invoke(NewtonsoftJsonSerializer.DefaultSettings);
        newtonsoftJsonSettings.Formatting = Formatting.Indented;
        SerializationTestExt.Register(
            new TestSerializer("NewtonsoftJson", () => new NewtonsoftJsonSerializer(newtonsoftJsonSettings)));
#if NET8_0_OR_GREATER
        SerializationTestExt.Register(
            new TestSerializer("NerdbankMessagePack", () => NerdbankMessagePackByteSerializer.Default) {
                MustPassUniSerialized = false,
                MustPassTypeDecorating = false,
                MustPassTypeEvolution = false,
            });
#endif
    }
}

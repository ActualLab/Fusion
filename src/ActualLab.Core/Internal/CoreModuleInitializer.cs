using ActualLab.Collections.Internal;
using ActualLab.Compliance.Internal;
using ActualLab.Serialization.Internal;
using ActualLab.Trimming;
using Cysharp.Serialization.MessagePack;

namespace ActualLab.Internal;

#pragma warning disable CA2255

/// <summary>
/// Module initializer that retains core types which are serialized
/// or resolved reflectively, so trimming / NativeAOT can't remove them.
/// </summary>
internal static class CoreModuleInitializer
{
    static CoreModuleInitializer()
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        // DefaultMessagePackResolver instantiates these reflectively, so nothing roots their constructors
        CodeKeeper.Keep<UnitMessagePackFormatter>();
        CodeKeeper.Keep<UlidMessagePackFormatter>();
        CodeKeeper.Keep<TypeDecoratingUniSerializedMessagePackFormatter<TypeSchema.Any, object>>();
        CodeKeeper.Keep<PropertyBagMessagePackFormatter<TypeSchema.Any>>();
        CodeKeeper.Keep<PropertyBagItemMessagePackFormatter<TypeSchema.Any>>();
        // [MessagePackFormatter(typeof(...))] formatters: MessagePack creates them reflectively, and the attribute
        // doesn't root their constructors, so NativeAOT trims them (msgpack6 is the default RPC format)
        CodeKeeper.Keep<ActualLab.Reflection.Internal.TypeRefMessagePackFormatter>();
        CodeKeeper.Keep<ActualLab.Text.Internal.SymbolMessagePackFormatter>();
        CodeKeeper.Keep<ActualLab.Text.Internal.ByteStringMessagePackFormatter>();
        CodeKeeper.Keep<ActualLab.Time.Internal.MomentMessagePackFormatter>();
        CodeKeeper.Keep<ActualLab.Time.Internal.CpuTimestampMessagePackFormatter>();
        CodeKeeper.Keep<ActualLab.IO.Internal.FilePathMessagePackFormatter>();
        CodeKeeper.Keep<HostIdMessagePackFormatter>();
        CodeKeeper.Keep<JsonStringMessagePackFormatter>();
        CodeKeeper.Keep<MessagePackDataMessagePackFormatter>();
        CodeKeeper.Keep(typeof(OptionMessagePackFormatter<>));
        CodeKeeper.Keep(typeof(ResultMessagePackFormatter<>));
        CodeKeeper.Keep(typeof(BoxMessagePackFormatter<>));
        CodeKeeper.Keep(typeof(MutableBoxMessagePackFormatter<>));
        CodeKeeper.Keep(typeof(PropertyBagMessagePackFormatter<>));
        CodeKeeper.Keep(typeof(PropertyBagItemMessagePackFormatter<>));
        CodeKeeper.Keep(typeof(TypeDecoratingUniSerializedMessagePackFormatter<,>));
        CodeKeeper.Keep(typeof(ImmutableBimapMessagePackFormatter<,>));
        CodeKeeper.Keep(typeof(SanitizedStringMessagePackFormatter<>));
        CodeKeeper.Keep(typeof(ActualLab.Api.Internal.ApiOptionMessagePackFormatter<>));
        CodeKeeper.Keep(typeof(ActualLab.Api.Internal.ApiNullableMessagePackFormatter<>));
        CodeKeeper.Keep(typeof(ActualLab.Api.Internal.ApiNullable8MessagePackFormatter<>));
        CodeKeeper.Keep(typeof(ActualLab.Api.Internal.ApiArrayMessagePackFormatter<>));
        // Every RPC handshake carries it, and reflection-based System.Text.Json (json5) needs its [JsonConstructor]
        CodeKeeper.Keep<VersionSet>();

        CodeKeeper.KeepSerializable<Unit>();
        CodeKeeper.KeepSerializable<PropertyBag>();
        CodeKeeper.KeepSerializable<MutablePropertyBag>();
        CodeKeeper.KeepSerializable<PropertyBagItem>();
        CodeKeeper.KeepSerializable<TypeDecoratingUniSerialized<TypeSchema.Any, object>>();

#if NET8_0_OR_GREATER
        // The generated resolver closes this via MakeGenericType, which ILC can't follow, and
        // KeepSerializable<T> above doesn't reach it - it keeps the serializers, not the formatters
        CodeKeeper.Keep<global::MessagePack.GeneratedMessagePackResolver.ActualLab.Collections.MutablePropertyBagFormatter<TypeSchema.Any>>();
        // Both bags serialize their items as PropertyBagItem[], whose formatter MessagePack's own
        // generic resolver closes reflectively as well
        CodeKeeper.Keep<global::MessagePack.Formatters.ArrayFormatter<PropertyBagItem>>();
#endif
    }

#if NET8_0_OR_GREATER
    [ModuleInitializer]
#endif
    internal static void Touch()
    { }
}

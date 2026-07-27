using ActualLab.Collections.Internal;
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

        CodeKeeper.KeepSerializable<Unit>();
        CodeKeeper.KeepSerializable<PropertyBag>();
        CodeKeeper.KeepSerializable<MutablePropertyBag>();
        CodeKeeper.KeepSerializable<PropertyBagItem>();
        CodeKeeper.KeepSerializable<TypeDecoratingUniSerialized<TypeSchema.Any, object>>();

#if NET8_0_OR_GREATER
        // These formatters are generic since TypeSchema was introduced, so the generated resolver
        // closes them via MakeGenericType - ILC won't emit them unless the closed types are rooted
        CodeKeeper.Keep<global::MessagePack.GeneratedMessagePackResolver.ActualLab.Collections.PropertyBagFormatter<TypeSchema.Any>>();
        CodeKeeper.Keep<global::MessagePack.GeneratedMessagePackResolver.ActualLab.Collections.MutablePropertyBagFormatter<TypeSchema.Any>>();
        CodeKeeper.Keep<global::MessagePack.GeneratedMessagePackResolver.ActualLab.Collections.Internal.PropertyBagItemFormatter<TypeSchema.Any>>();
#endif
    }

#if NET8_0_OR_GREATER
    [ModuleInitializer]
#endif
    internal static void Touch()
    { }
}

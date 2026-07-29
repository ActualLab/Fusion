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
        // Not a trimming concern, and must run unconditionally: MemoryPack resolves a formatter
        // by type without touching the type, so SanitizedString<>'s own static constructor can
        // be too late. Registering the open generic here covers every instantiation.
#if !NETSTANDARD2_0
        SanitizedStringMemoryPackFormatter.RegisterGenericType();
#endif

        if (CodeKeeper.AlwaysTrue)
            return;

        // DefaultMessagePackResolver instantiates these reflectively, so nothing roots their constructors
        CodeKeeper.Keep<UnitMessagePackFormatter>();
        CodeKeeper.Keep<UlidMessagePackFormatter>();
        CodeKeeper.Keep<TypeDecoratingUniSerializedMessagePackFormatter<TypeSchema.Any, object>>();
        CodeKeeper.Keep<PropertyBagMessagePackFormatter<TypeSchema.Any>>();
        CodeKeeper.Keep<PropertyBagItemMessagePackFormatter<TypeSchema.Any>>();

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

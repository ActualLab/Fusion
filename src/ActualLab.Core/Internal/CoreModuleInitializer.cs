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
        // The generated resolver closes these via MakeGenericType, which ILC can't follow, and
        // KeepSerializable<T> above doesn't reach them - it keeps the serializers, not the formatters
        CodeKeeper.Keep<global::MessagePack.GeneratedMessagePackResolver.ActualLab.Collections.PropertyBagFormatter<TypeSchema.Any>>();
        CodeKeeper.Keep<global::MessagePack.GeneratedMessagePackResolver.ActualLab.Collections.MutablePropertyBagFormatter<TypeSchema.Any>>();
        CodeKeeper.Keep<global::MessagePack.GeneratedMessagePackResolver.ActualLab.Collections.Internal.PropertyBagItemFormatter<TypeSchema.Any>>();
        CodeKeeper.Keep<global::MessagePack.GeneratedMessagePackResolver.ActualLab.Serialization.TypeDecoratingUniSerializedFormatter<TypeSchema.Any, object>>();
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

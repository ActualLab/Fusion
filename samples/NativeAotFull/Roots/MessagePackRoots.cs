using ActualLab.Api;
using ActualLab.Api.Internal;
using ActualLab.Collections.Internal;
using ActualLab.Compliance;
using ActualLab.Compliance.Internal;
using ActualLab.Serialization.Internal;
using ActualLab.Trimming;
using MessagePackFormatters = MessagePack.Formatters;

// The closed formatters MessagePack's resolvers create via MakeGenericType. ActualLab.Generators emits
// these roots for assemblies that define ACTUALLAB_SERIALIZATION_ROOTS (see the project file), so Program.cs
// doesn't call Keep: it shows what the generated ActualLabSerializationRoots class does.
// Like CodeKeeper's own: the roots never execute, they only make the AOT compiler keep the code
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The roots only keep the code")]
[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The roots only keep the code")]
public static class MessagePackRoots
{
    public static void Keep()
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        // [MessagePackFormatter(typeof(...<>))] formatters of ActualLab types
        CodeKeeper.Keep<OptionMessagePackFormatter<int>>();
        CodeKeeper.Keep<OptionMessagePackFormatter<long>>(); // PageRef<long>.After
        CodeKeeper.Keep<ApiArrayMessagePackFormatter<int>>();
        CodeKeeper.Keep<ApiOptionMessagePackFormatter<int>>();
        CodeKeeper.Keep<ApiNullableMessagePackFormatter<int>>();
        CodeKeeper.Keep<ApiNullable8MessagePackFormatter<long>>();
        CodeKeeper.Keep<SanitizedStringMessagePackFormatter<Sanitizers.Hidden>>();
        CodeKeeper.Keep<SanitizedStringMessagePackFormatter<CustomSanitizer>>();
        CodeKeeper.Keep<PropertyBagMessagePackFormatter<CustomSchema>>();
        CodeKeeper.Keep<PropertyBagItemMessagePackFormatter<CustomSchema>>();
        CodeKeeper.Keep<TypeDecoratingUniSerializedMessagePackFormatter<CustomSchema, object>>();
        // The formatter MessagePack's source generator creates for a generic [MessagePackObject] type is internal
        CodeKeeper.Keep("MessagePack.GeneratedMessagePackResolver+ActualLab+Fusion+Extensions+PageRefFormatter`1"
            + "[[System.Int64, System.Runtime]], ActualLab.Fusion.Ext.Contracts");
        // MessagePack's own generic formatters
        CodeKeeper.Keep<MessagePackFormatters.ArrayFormatter<Moment>>();
        CodeKeeper.Keep<MessagePackFormatters.ArrayFormatter<PropertyBagItem<CustomSchema>>>();
        CodeKeeper.Keep<MessagePackFormatters.ListFormatter<int>>();
        CodeKeeper.Keep<MessagePackFormatters.ListFormatter<Moment>>();
        CodeKeeper.Keep<MessagePackFormatters.DictionaryFormatter<string, Moment>>();
        CodeKeeper.Keep<MessagePackFormatters.NullableFormatter<int>>();
        CodeKeeper.Keep<MessagePackFormatters.NullableFormatter<Moment>>();
    }
}

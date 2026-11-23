using ActualLab.Api;
using ActualLab.Collections.Internal;
using ActualLab.Compliance;
using ActualLab.Fusion.Extensions;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Serialization.Internal;
using ActualLab.Trimming;
using PolyType;

// Nerdbank.MessagePack needs, under NativeAOT and without ActualLab.Generators' help:
// - Register: source-generated PolyType shapes (NerdbankShapes below) instead of reflection-based ones;
// - the TypeShapeExtension attributes below, which pair ActualLab's types with their converters;
// - Keep: the typed serializers NerdbankMessagePackByteSerializer creates via MakeGenericType.
[assembly: TypeShapeExtension(typeof(Option<>), AssociatedTypes = [typeof(OptionNerdbankConverter<>)])]
[assembly: TypeShapeExtension(typeof(ApiArray<>), AssociatedTypes = [typeof(ApiArrayNerdbankConverter<>)])]
[assembly: TypeShapeExtension(typeof(PropertyBag<>), AssociatedTypes = [typeof(PropertyBagNerdbankConverter<>)])]
[assembly: TypeShapeExtension(typeof(PropertyBagItem<>),
    AssociatedTypes = [typeof(PropertyBagItemNerdbankConverter<>)])]
[assembly: TypeShapeExtension(typeof(SanitizedString<>),
    AssociatedTypes = [typeof(SanitizedStringNerdbankConverter<>)])]
[assembly: TypeShapeExtension(typeof(TypeDecoratingUniSerialized<,>),
    AssociatedTypes = [typeof(TypeDecoratingUniSerializedNerdbankConverter<,>)])]

// Like CodeKeeper's own: the roots never execute, they only make the AOT compiler keep the code
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The roots only keep the code")]
[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The roots only keep the code")]
public static class NerdbankRoots
{
    public static void Register()
        => NerdbankMessagePackByteSerializer.DefaultTypeShapeProvider = NerdbankShapes.GeneratedTypeShapeProvider;

    public static void Keep()
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        CodeKeeper.Keep<NerdbankMessagePackByteSerializer<int>>();
        CodeKeeper.Keep<NerdbankMessagePackByteSerializer<int?>>();
        CodeKeeper.Keep<NerdbankMessagePackByteSerializer<long>>();
        CodeKeeper.Keep<NerdbankMessagePackByteSerializer<Guid>>();
        CodeKeeper.Keep<NerdbankMessagePackByteSerializer<Unit>>();
        CodeKeeper.Keep<NerdbankMessagePackByteSerializer<RpcNoWait>>();
        CodeKeeper.Keep<NerdbankMessagePackByteSerializer<Moment>>();
        CodeKeeper.Keep<NerdbankMessagePackByteSerializer<Moment?>>();
        CodeKeeper.Keep<NerdbankMessagePackByteSerializer<Option<int>>>();
        CodeKeeper.Keep<NerdbankMessagePackByteSerializer<ApiArray<int>>>();
        CodeKeeper.Keep<NerdbankMessagePackByteSerializer<PropertyBag<TypeSchema.Any>>>();
        CodeKeeper.Keep<NerdbankMessagePackByteSerializer<PropertyBag<CustomSchema>>>();
        CodeKeeper.Keep<NerdbankMessagePackByteSerializer<SanitizedString<Sanitizers.Hidden>>>();
        CodeKeeper.Keep<NerdbankMessagePackByteSerializer<SanitizedString<CustomSanitizer>>>();
    }
}

// Nerdbank.MessagePack needs a PolyType shape for every type it serializes, and NativeAOT has no
// reflection-based shapes, so PolyType's source generator makes them for the types listed here.
// NerdbankRoots.Register makes NerdbankMessagePackByteSerializer use them.
// NerdbankMessagePackByteSerializer closes its generic converters for ActualLab types (its ConverterTypes)
// through these shapes, so the generator has to know which converter goes with which type.
[GenerateShapeFor<RpcHandshake>]
[GenerateShapeFor<RpcNoWait>]
[GenerateShapeFor<ExceptionInfo>]
[GenerateShapeFor<Unit>]
[GenerateShapeFor<Guid>]
[GenerateShapeFor<long[]>]
[GenerateShapeFor<byte[]>]
[GenerateShapeFor<Dictionary<int, byte[]>>]
[GenerateShapeFor<Moment>]
[GenerateShapeFor<string>]
[GenerateShapeFor<SayHelloCommand>]
[GenerateShapeFor<PropertyBag<TypeSchema.Any>>]
[GenerateShapeFor<PropertyBag<CustomSchema>>]
[GenerateShapeFor<SanitizedString<Sanitizers.Hidden>>]
[GenerateShapeFor<SanitizedString<CustomSanitizer>>]
[GenerateShapeFor<Option<int>>]
[GenerateShapeFor<int?>]
[GenerateShapeFor<List<int>>]
[GenerateShapeFor<Moment?>]
[GenerateShapeFor<Moment[]>]
[GenerateShapeFor<List<Moment>>]
[GenerateShapeFor<Dictionary<string, Moment>>]
[GenerateShapeFor<ApiArray<int>>]
[GenerateShapeFor<PageRef<long>>]
public partial class NerdbankShapes;

using System.Text.Json.Serialization;
using ActualLab.Api;
using ActualLab.Api.Internal;
using ActualLab.Compliance;
using ActualLab.Compliance.Internal;
using ActualLab.Fusion.Extensions;
using ActualLab.Trimming;

// System.Text.Json needs two things under NativeAOT:
// - Register: source-generated metadata for the app's types (AppJsonContext below), added before
//   they're first serialized - System.Text.Json caches each lookup, including a failed one;
// - Keep: the closed converters the [JsonConverter] factories of ActualLab types create via MakeGenericType.
//   ActualLab.Generators emits these roots for assemblies that define ACTUALLAB_SERIALIZATION_ROOTS
//   (see the project file), so Program.cs doesn't call Keep.
// Like CodeKeeper's own: the roots never execute, they only make the AOT compiler keep the code
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The roots only keep the code")]
[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The roots only keep the code")]
public static class SystemJsonRoots
{
    public static void Register()
        => SystemJsonSerializer.TypeInfoResolver.Add(AppJsonContext.Default);

    public static void Keep()
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        CodeKeeper.Keep<ApiArrayJsonConverter.Converter<int>>();
        CodeKeeper.Keep<SanitizedStringJsonConverter.Converter<Sanitizers.Hidden>>();
        CodeKeeper.Keep<SanitizedStringJsonConverter.Converter<CustomSanitizer>>();
        // UniSerializedJsonConverter's converters are private
        CodeKeeper.Keep("ActualLab.Serialization.Internal.UniSerializedJsonConverter+TypeDecoratingConverter`2"
            + "[[CustomSchema, Samples.NativeAotFull],[System.Object, System.Runtime]], ActualLab.Core");
    }
}

// NativeAOT has no reflection-based System.Text.Json, so the json5 formats need the metadata
// of every type the RPC calls carry; ActualLab's own types and primitives are covered already.
// SystemJsonRoots.Register adds it to SystemJsonSerializer.TypeInfoResolver.
[JsonSerializable(typeof(SayHelloCommand))]
[JsonSerializable(typeof(ApiDto))]
[JsonSerializable(typeof(PropertyBag<CustomSchema>))]
[JsonSerializable(typeof(SanitizedString<Sanitizers.Hidden>))]
[JsonSerializable(typeof(SanitizedString<CustomSanitizer>))]
[JsonSerializable(typeof(Option<int>))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(List<int>))]
[JsonSerializable(typeof(Moment?))]
[JsonSerializable(typeof(Moment[]))]
[JsonSerializable(typeof(List<Moment>))]
[JsonSerializable(typeof(Dictionary<string, Moment>))]
[JsonSerializable(typeof(ApiArray<int>))]
[JsonSerializable(typeof(PageRef<long>))]
[JsonSerializable(typeof(ApiOption<int>))]
[JsonSerializable(typeof(ApiNullable<int>))]
[JsonSerializable(typeof(ApiNullable8<long>))]
public sealed partial class AppJsonContext : JsonSerializerContext;

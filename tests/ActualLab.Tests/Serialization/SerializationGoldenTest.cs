#if NET8_0_OR_GREATER
using ActualLab.Caching;
using ActualLab.CommandR.Operations;
using ActualLab.Compliance;
using ActualLab.Interception;
using ActualLab.IO;
using ActualLab.Plugins.Metadata;
using ActualLab.Redis;
using ActualLab.Reflection;
using ActualLab.Rpc;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Infrastructure;
using MessagePack;
using StackExchange.Redis;

namespace ActualLab.Tests.Serialization;

public class SerializationGoldenTest(ITestOutputHelper @out) : GoldenSerializationTestBase(@out)
{
    private static readonly Moment FixedMoment = new(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc));
    private static readonly Guid FixedGuid1 = new("0f8fad5b-d9cb-469f-a165-70867728950e");
    private static readonly Guid FixedGuid2 = new("7c9e6679-7425-40de-944b-e07fc1f90ae7");
    private static readonly TypeRef IntTypeRef = new TypeRef(typeof(int)).WithoutAssemblyVersions();

    [Fact]
    public void OutputShouldMatchGolden()
        => AssertOutputMatchesGolden();

    protected override IEnumerable<GoldenCase> GetCases()
        => CoreCases()
            .Concat(ApiCases())
            .Concat(WrapperCases())
            .Concat(RpcAndCommandRCases())
            .Concat(DtoCases());

    // Private methods

    private static IEnumerable<GoldenCase> CoreCases()
    {
        yield return Case("Symbol.Default", default(Symbol));
        yield return Case("Symbol.Value", new Symbol("abc"));
        yield return Case("ByteString.Default", default(ByteString));
        yield return Case("ByteString.Value", new ByteString([1, 2, 3, 255]));
        yield return Case("TypeRef.Default", default(TypeRef));
        yield return Case("TypeRef.Int", IntTypeRef);
        yield return Case("TypeRef.String", new TypeRef("ActualLab.Text.Symbol, ActualLab.Core"));
        yield return Case("FilePath.Default", default(FilePath));
        yield return Case("FilePath.Value", FilePath.New("dir/file.txt"));
        yield return Case("HostId.Null", (HostId?)null);
        yield return Case("HostId.Value", new HostId("golden-host"));
        yield return Case("JsonString.Null", (JsonString?)null);
        yield return Case("JsonString.Empty", new JsonString(""));
        yield return Case("JsonString.Value", new JsonString("{\"a\":1}"));
        yield return Case("TextOrBytes.Default", default(TextOrBytes));
        yield return Case("TextOrBytes.EmptyText", TextOrBytes.EmptyText);
        yield return Case("TextOrBytes.Text", new TextOrBytes("text"));
        yield return Case("TextOrBytes.Bytes", new TextOrBytes(new byte[] { 1, 2, 3 }));
        yield return Case("ExceptionInfo.Default", default(ExceptionInfo));
        yield return Case("ExceptionInfo.Value", new ExceptionInfo(new InvalidOperationException("Fail!")));
        yield return Case("Moment.Default", default(Moment));
        yield return Case("Moment.Value", FixedMoment);
        // System.Text.Json also writes the time-dependent Elapsed property
        yield return Case("CpuTimestamp.Value", new CpuTimestamp(1234567890), "SystemJson");
        yield return Case("RandomTimeSpan.Default", default(RandomTimeSpan));
        yield return Case("RandomTimeSpan.Value",
            new RandomTimeSpan(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(100)));
        yield return Case("SanitizedString.Empty", default(SanitizedString<Sanitizers.Hidden>));
        yield return Case("SanitizedString.Value", new SanitizedString<Sanitizers.Hidden>("secret"));
        yield return Case("VoidSurrogate.Default", default(VoidSurrogate));
        yield return Case("VersionSet.Empty", VersionSet.Empty);
        yield return Case("VersionSet.Value", new VersionSet(("a", "1.0"), ("b", "2.0.1")));
        yield return Case("ImmutableBimap.Empty", new ImmutableBimap<int, string>());
        yield return Case("ImmutableBimap.Value", new ImmutableBimap<int, string>(
            new Dictionary<int, string> { [1] = "one", [2] = "two" }));
        yield return Case("Box.Int", Box.New(5));
        yield return Case("Box.StringNull", Box.New((string?)null));
        yield return Case("MutableBox.Int", MutableBox.New(5));
        yield return Case("MutableBox.String", MutableBox.New("abc"));
        yield return Case("PropertyBag.Empty", new PropertyBag());
        yield return Case("PropertyBag.Value", NewPropertyBag());
        yield return Case("MutablePropertyBag.Empty", new MutablePropertyBag());
        yield return Case("MutablePropertyBag.Value", new MutablePropertyBag(NewPropertyBag()));
        yield return Case("PropertyBagItem.Value", new PropertyBagItem("Key", TypeDecoratingUniSerialized
            .New<TypeSchema.Any, object>(5)));
        yield return Case("RedisSubKey.Value", new RedisSubKey("key", RedisChannel.PatternMode.Literal));
        yield return Case("ArgumentList.IntString", ArgumentList.New(5, "x"));
        yield return Case("PluginSetInfo.Empty", PluginSetInfo.Empty);
        yield return Case("PluginInfo.Value", new PluginInfo(
            IntTypeRef,
            [new TypeRef(typeof(object)).WithoutAssemblyVersions()],
            [],
            [],
            new PropertyBag().Set("Capability", 1),
            [],
            [],
            0));
    }

    private static IEnumerable<GoldenCase> ApiCases()
    {
        yield return Case("Option<int>.None", Option.None<int>());
        yield return Case("Option<int>.Some", Option.Some(5));
        yield return Case("Option<string>.None", Option.None<string>());
        yield return Case("Option<string>.Some", Option.Some("abc"));
        yield return Case("Result<Option<int>>.Some", Result.New(Option.Some(5)));
        yield return Case("Tuple<Option<int>,string>.Some", (Option.Some(5), "x"));
        yield return Case("Dictionary<string,Option<int>>", new Dictionary<string, Option<int>> {
            ["a"] = Option.Some(5),
            ["b"] = Option.None<int>(),
        });

        yield return Case("ApiOption<int>.None", ApiOption.None<int>(), "Nerdbank");
        yield return Case("ApiOption<int>.Some", ApiOption.Some(5), "Nerdbank");
        yield return Case("ApiOption<string>.None", ApiOption.None<string>(), "Nerdbank");
        yield return Case("ApiOption<string>.Some", ApiOption.Some("abc"), "Nerdbank");
        yield return Case("Result<ApiOption<int>>.Some", Result.New(ApiOption.Some(5)), "Nerdbank");
        yield return Case("Tuple<ApiOption<int>,string>.Some", (ApiOption.Some(5), "x"), "Nerdbank");
        yield return Case("Dictionary<string,ApiOption<int>>", new Dictionary<string, ApiOption<int>> {
            ["a"] = ApiOption.Some(5),
            ["b"] = ApiOption.None<int>(),
        }, "Nerdbank");

        yield return Case("ApiNullable<int>.Null", ApiNullable.Null<int>(), "Nerdbank");
        yield return Case("ApiNullable<int>.Value", ApiNullable.Value(5), "Nerdbank");
        yield return Case("ApiNullable<long>.Value", ApiNullable.Value(1234567890123L), "Nerdbank");
        yield return Case("Result<ApiNullable<int>>.Value", Result.New(ApiNullable.Value(5)), "Nerdbank");
        yield return Case("Tuple<ApiNullable<int>,string>.Value", (ApiNullable.Value(5), "x"), "Nerdbank");
        yield return Case("Dictionary<string,ApiNullable<int>>", new Dictionary<string, ApiNullable<int>> {
            ["a"] = ApiNullable.Value(5),
            ["b"] = ApiNullable.Null<int>(),
        }, "Nerdbank");

        yield return Case("ApiNullable8<int>.Null", ApiNullable8.Null<int>(), "Nerdbank");
        yield return Case("ApiNullable8<int>.Value", ApiNullable8.Value(5), "Nerdbank");
        yield return Case("ApiNullable8<long>.Value", ApiNullable8.Value(1234567890123L), "Nerdbank");
        yield return Case("Result<ApiNullable8<int>>.Value", Result.New(ApiNullable8.Value(5)), "Nerdbank");
        yield return Case("Tuple<ApiNullable8<int>,string>.Value", (ApiNullable8.Value(5), "x"), "Nerdbank");
        yield return Case("Dictionary<string,ApiNullable8<int>>", new Dictionary<string, ApiNullable8<int>> {
            ["a"] = ApiNullable8.Value(5),
            ["b"] = ApiNullable8.Null<int>(),
        }, "Nerdbank");

        yield return Case("Result<int>.Default", default(Result<int>));
        yield return Case("Result<int>.Value", Result.New(5));
        yield return Case("Result<string>.Value", Result.New("abc"));
        yield return Case("Result<int>.Error", Result.NewError<int>(new InvalidOperationException("Fail!")));

        yield return Case("ApiArray<string>.Empty", ApiArray.Empty<string>());
        yield return Case("ApiArray<string>.Value", ApiArray.New("a", "b"));
        yield return Case("ApiArray<int>.Value", ApiArray.New(1, 2, 3));
        yield return Case("ApiList<string>.Empty", new ApiList<string>());
        yield return Case("ApiList<string>.Value", new ApiList<string> { "b", "a" });
        yield return Case("ApiSet<string>.Empty", new ApiSet<string>());
        yield return Case("ApiSet<string>.Value", new ApiSet<string> { "b", "a", "c" });
        yield return Case("ApiSet<int>.Value", new ApiSet<int> { 3, 1, 2 });
        yield return Case("ApiMap<string,int>.Empty", new ApiMap<string, int>());
        yield return Case("ApiMap<string,int>.Value", new ApiMap<string, int> { ["b"] = 2, ["a"] = 1, ["c"] = 3 });
        yield return Case("ApiMap<int,string>.Value", new ApiMap<int, string> { [2] = "two", [1] = "one" });
    }

    private static IEnumerable<GoldenCase> WrapperCases()
    {
        yield return Case("UniSerialized<int>", UniSerialized.New(5));
        yield return Case("UniSerialized<string>", UniSerialized.New("abc"));
        yield return Case("UniSerialized<Option<int>>", UniSerialized.New(Option.Some(5)));
        yield return Case("TypeDecoratingUniSerialized<Any,object>.Null",
            TypeDecoratingUniSerialized.New<TypeSchema.Any, object?>(null));
        yield return Case("TypeDecoratingUniSerialized<Any,object>.Int",
            TypeDecoratingUniSerialized.New<TypeSchema.Any, object?>(5));
        yield return Case("TypeDecoratingUniSerialized<Any,object>.String",
            TypeDecoratingUniSerialized.New<TypeSchema.Any, object?>("abc"));
        yield return Case("TypeDecoratingUniSerialized<PrimitiveOnly,object>.Symbol",
            TypeDecoratingUniSerialized.New<TypeSchema.PrimitiveOnly, object?>(new Symbol("abc")));
        yield return Case("ByteSerialized<int>", ByteSerialized.New(5));
        yield return Case("TextSerialized<int>", TextSerialized.New(5));
        yield return Case("MemoryPackSerialized<int>", MemoryPackSerialized.New(5));
        yield return Case("MemoryPackSerialized<string>", MemoryPackSerialized.New("abc"));
        yield return Case("MessagePackSerialized<int>", MessagePackSerialized.New(5));
        yield return Case("NewtonsoftJsonSerialized<int>", NewtonsoftJsonSerialized.New(5));
        yield return Case("SystemJsonSerialized<int>", SystemJsonSerialized.New(5));
        yield return Case("NerdbankMessagePackSerialized<int>", NerdbankMessagePackSerialized.New(5));
        yield return Case("TypeDecoratingMemoryPackSerialized<Any,object>",
            TypeDecoratingMemoryPackSerialized.New<TypeSchema.Any, object>(5));
        yield return Case("TypeDecoratingMessagePackSerialized<Any,object>",
            TypeDecoratingMessagePackSerialized.New<TypeSchema.Any, object>(5));
        yield return Case("TypeDecoratingNewtonsoftJsonSerialized<Any,object>",
            TypeDecoratingNewtonsoftJsonSerialized.New<TypeSchema.Any, object>(5));
        yield return Case("TypeDecoratingSystemJsonSerialized<Any,object>",
            TypeDecoratingSystemJsonSerialized.New<TypeSchema.Any, object>(5));
        yield return Case("TypeDecoratingNerdbankMessagePackSerialized<object>",
            TypeDecoratingNerdbankMessagePackSerialized.New<object>(5));
    }

    private static IEnumerable<GoldenCase> RpcAndCommandRCases()
    {
        yield return Case("RpcMethodRef.Default", default(RpcMethodRef));
        yield return Case("RpcMethodRef.Value", new RpcMethodRef("GoldenService.Get:2"));
        yield return Case("RpcHeaderKey.Default", default(RpcHeaderKey));
        yield return Case("RpcHeaderKey.Value", new RpcHeaderKey("x-golden"));
        yield return Case("RpcHeader.Default", default(RpcHeader));
        yield return Case("RpcHeader.Value", new RpcHeader("x-golden", "value"));
        yield return Case("RpcHandshake.Value", new RpcHandshake(
            FixedGuid1, new VersionSet("api", "1.0"), FixedGuid2, RpcHandshake.CurrentProtocolVersion, 3, "secret"));
        yield return Case("RpcHandshake.NullVersionSet", new RpcHandshake(FixedGuid1, null, FixedGuid2, 2, 0));
        yield return Case("RpcCacheKey.Value", new RpcCacheKey("GoldenService.Get", new byte[] { 1, 2, 3 }));
        yield return Case("RpcCacheValue.Value", new RpcCacheValue(new byte[] { 4, 5, 6 }, "hash"));
        yield return Case("RpcCacheValue.RequestHash", RpcCacheValue.RequestHash);
        yield return Case("RpcObjectId.Default", default(RpcObjectId));
        yield return Case("RpcObjectId.Value", new RpcObjectId(FixedGuid1, 42));
        yield return Case("RpcNoWait.Default", default(RpcNoWait));

        var serviceCall = NewServiceCall();
        yield return Case("ServiceCall.Value", serviceCall);
        yield return Case("OperationCompletion.NoCommand", OperationCompletion.New(null, [serviceCall]));
        yield return Case("OperationCompletion.Command",
            OperationCompletion.New(new GoldenCommand("cmd-1"), [serviceCall]));
    }

    private static IEnumerable<GoldenCase> DtoCases()
    {
        yield return Case("GoldenDto.Default", new GoldenDto());
        yield return Case("GoldenDto.Value", new GoldenDto {
            Symbol = new Symbol("sym"),
            IntOption = Option.Some(5),
            StringOption = Option.Some("abc"),
            ApiIntOption = ApiOption.Some(6),
            ApiNullable = ApiNullable.Value(7),
            ApiNullable8 = ApiNullable8.Value(8L),
            Items = ApiArray.New("a", "b"),
            TypeRef = IntTypeRef,
            Moment = FixedMoment,
            Bytes = new ByteString([1, 2]),
            FilePath = FilePath.New("dir/file.txt"),
            Bag = new PropertyBag().Set("Int", 1),
            VersionSet = new VersionSet("a", "1.0"),
            Error = new ExceptionInfo(new InvalidOperationException("Fail!")),
            Result = Result.New(9),
            Map = new ApiMap<string, int> { ["a"] = 1 },
            HostId = new HostId("golden-host"),
            Tuple = (Option.Some(10), "x"),
        });
        yield return Case("GoldenCompactDto.Default",
            new GoldenCompactDto(default, default, default, default, default));
        yield return Case("GoldenCompactDto.Value", new GoldenCompactDto(
            new Symbol("sym"), Option.Some(5), ApiOption.Some(6), ApiNullable.Value(7), ApiNullable8.Value(8L)));
        yield return Case("GoldenGenericDto<Option<int>>.Some", new GoldenGenericDto<Option<int>>(Option.Some(5)));
        yield return Case("GoldenGenericDto<Option<int>>.None", new GoldenGenericDto<Option<int>>(default));
        yield return Case("GoldenGenericDto<ApiOption<int>>.Some",
            new GoldenGenericDto<ApiOption<int>>(ApiOption.Some(5)), "Nerdbank");
        yield return Case("GoldenGenericDto<ApiNullable<int>>.Value",
            new GoldenGenericDto<ApiNullable<int>>(ApiNullable.Value(5)), "Nerdbank");
        yield return Case("GoldenGenericDto<ApiNullable8<int>>.Value",
            new GoldenGenericDto<ApiNullable8<int>>(ApiNullable8.Value(5)), "Nerdbank");
        yield return Case("GoldenGenericDto<Symbol>", new GoldenGenericDto<Symbol>(new Symbol("sym")));
    }

    private static PropertyBag NewPropertyBag()
        => new PropertyBag()
            .Set("Int", 1)
            .Set("String", "x")
            .Set("Moment", FixedMoment)
            .Set("Symbol", new Symbol("sym"))
            .Set("Box", Box.New(5));

    private static ServiceCall NewServiceCall()
    {
        var method = typeof(IGoldenService).GetMethod(nameof(IGoldenService.Get))!;
        return ServiceCall.New(typeof(IGoldenService), method, ArgumentList.New("ab", default(CancellationToken)));
    }
}

public interface IGoldenService
{
    public Task<string> Get(string key, CancellationToken cancellationToken = default);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
public sealed partial record GoldenCommand(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] string Id
) : ICommand<Unit>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
public sealed partial record GoldenDto
{
    [DataMember(Order = 0), MemoryPackOrder(0), MemoryPackAllowSerialize, Key(0)] public Symbol Symbol { get; init; }
    // Ignored by MemoryPack: it copies an unmanaged Option<T> as raw memory, padding included,
    // so the output isn't deterministic - ApiIntOption is what MemoryPack contracts should use
    [DataMember(Order = 1), MemoryPackIgnore, Key(1)] public Option<int> IntOption { get; init; }
    [DataMember(Order = 2), MemoryPackOrder(2), MemoryPackAllowSerialize, Key(2)] public Option<string> StringOption { get; init; }
    [DataMember(Order = 3), MemoryPackOrder(3), Key(3)] public ApiOption<int> ApiIntOption { get; init; }
    [DataMember(Order = 4), MemoryPackOrder(4), Key(4)] public ApiNullable<int> ApiNullable { get; init; }
    [DataMember(Order = 5), MemoryPackOrder(5), Key(5)] public ApiNullable8<long> ApiNullable8 { get; init; }
    [DataMember(Order = 6), MemoryPackOrder(6), Key(6)] public ApiArray<string> Items { get; init; }
    [DataMember(Order = 7), MemoryPackOrder(7), MemoryPackAllowSerialize, Key(7)] public TypeRef TypeRef { get; init; }
    [DataMember(Order = 8), MemoryPackOrder(8), Key(8)] public Moment Moment { get; init; }
    [DataMember(Order = 9), MemoryPackOrder(9), MemoryPackAllowSerialize, Key(9)] public ByteString Bytes { get; init; }
    [DataMember(Order = 10), MemoryPackOrder(10), MemoryPackAllowSerialize, Key(10)] public FilePath FilePath { get; init; }
    [DataMember(Order = 11), MemoryPackOrder(11), MemoryPackAllowSerialize, Key(11)] public PropertyBag Bag { get; init; }
    [DataMember(Order = 12), MemoryPackOrder(12), MemoryPackAllowSerialize, Key(12)] public VersionSet? VersionSet { get; init; }
    [DataMember(Order = 13), MemoryPackOrder(13), MemoryPackAllowSerialize, Key(13)] public ExceptionInfo Error { get; init; }
    [DataMember(Order = 14), MemoryPackOrder(14), MemoryPackAllowSerialize, Key(14)] public Result<int> Result { get; init; }
    [DataMember(Order = 15), MemoryPackOrder(15), Key(15)] public ApiMap<string, int>? Map { get; init; }
    [DataMember(Order = 16), MemoryPackOrder(16), MemoryPackAllowSerialize, Key(16)] public HostId? HostId { get; init; }
    [DataMember(Order = 17), MemoryPackOrder(17), Key(17)] public (Option<int>, string) Tuple { get; init; }
}

[DataContract, MemoryPackable, MessagePackObject]
public sealed partial record GoldenCompactDto(
    [property: DataMember(Order = 0), MemoryPackOrder(0), MemoryPackAllowSerialize, Key(0)] Symbol Symbol,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] Option<int> IntOption,
    [property: DataMember(Order = 2), MemoryPackOrder(2), Key(2)] ApiOption<int> ApiIntOption,
    [property: DataMember(Order = 3), MemoryPackOrder(3), Key(3)] ApiNullable<int> ApiNullable,
    [property: DataMember(Order = 4), MemoryPackOrder(4), Key(4)] ApiNullable8<long> ApiNullable8);

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
public sealed partial record GoldenGenericDto<T>(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] T Value);
#endif

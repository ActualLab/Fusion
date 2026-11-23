using System.Runtime.Serialization;
using ActualLab.Api;
using ActualLab.Compliance;
using ActualLab.Fusion.Extensions;
using ActualLab.Rpc;
using ActualLab.Rpc.Compression;
using ActualLab.Trimming;
using MemoryPack;
using MessagePack;
using static System.Console;

#pragma warning disable IL3050

// Calls a loopback RPC service with every wire format the serializer packages provide,
// round-tripping the types whose trimming / AOT support matters. Exit code 1 = a check failed.

KeepSerializableTypes();
var formatKeys = args.Length > 0 ? args : GetAllFormatKeys();
var failureCount = 0;
foreach (var formatKey in formatKeys)
    failureCount += await TestFormat(formatKey);
WriteLine(failureCount == 0 ? "All checks passed." : $"{failureCount} check(s) failed.");
return failureCount == 0 ? 0 : 1;

// What a NativeAOT app does for its contract types: KeepSerializable never runs its body,
// it only makes the AOT compiler generate the serialization code
static void KeepSerializableTypes()
{
    CodeKeeper.KeepSerializable<SayHelloCommand>();
    CodeKeeper.KeepSerializable<ApiDto>();
    CodeKeeper.KeepSerializable<PropertyBag<CustomSchema>>();
    CodeKeeper.KeepSerializable<SanitizedString<Sanitizers.Hidden>>();
    CodeKeeper.KeepSerializable<SanitizedString<CustomSanitizer>>();
    CodeKeeper.KeepSerializable<ApiOption<int>>();
    CodeKeeper.KeepSerializable<ApiNullable<int>>();
    CodeKeeper.KeepSerializable<ApiNullable8<long>>();
}

string[] GetAllFormatKeys()
{
    var services = CreateServices(null);
    return services.RpcHub().WireFormats.Formats.Keys.Order(StringComparer.Ordinal).ToArray();
}

ServiceProvider CreateServices(string? formatKey)
    => new ServiceCollection()
        .AddLogging(l => l.SetMinimumLevel(LogLevel.Error).AddSimpleConsole())
        .AddFusion(fusion => {
            fusion.Rpc.ConfigureWireFormats(f => {
                f.AddMemoryPack();
                f.AddNewtonsoftJson();
                f.AddNerdbankMessagePack();
                f.AddLZ4Variants(RpcCompressionMode.ServerToClient, RpcCompressionMode.Full);
                if (formatKey is not null)
                    f.DefaultFormat = f[formatKey];
            });
            fusion.Rpc.AddWebSocketClient();
            fusion.AddServerAndClient<ITestService, TestService>();
        })
        .AddSingleton(_ => RpcOutboundCallOptions.Default with {
            RouterFactory = _ => _ => RpcRef.Loopback,
        })
        .BuildServiceProvider();

async Task<int> TestFormat(string formatKey)
{
    WriteLine($"[{formatKey}]");
    await using var services = CreateServices(formatKey);
    var client = services.RpcHub().GetClient<ITestService>();
    // Nerdbank.MessagePack has no converters for the MemoryPack package's Api* types
    var hasApiTypes = !formatKey.StartsWith("nmsgpack", StringComparison.Ordinal);
    var failureCount = 0;

    await Check("Moment", async () => {
        var now = await client.GetTime();
        return now != default;
    });
    await Check("compute method", async () => (await client.GetTimeComputed()) != default);
    await Check("command", async () => await client.OnSayHello(new SayHelloCommand("AOT")) == "Hello, AOT");
    await Check("PropertyBag<TypeSchema.Any>", async () => {
        var bag = PropertyBag<TypeSchema.Any>.Empty.Set("Int", 1).Set("String", "x");
        var copy = await client.EchoBag(bag);
        return copy.Get<int>("Int") == 1 && copy.Get<string>("String") == "x";
    });
    await Check("PropertyBag<CustomSchema>", async () => {
        var bag = PropertyBag<CustomSchema>.Empty.Set("Int", 1);
        var copy = await client.EchoCustomBag(bag);
        return copy["Int"] is int i && i == 1;
    });
    await Check("SanitizedString<Hidden>", async () => {
        var value = new SanitizedString<Sanitizers.Hidden>("secret");
        return (await client.EchoSanitized(value)).Value == "secret";
    });
    await Check("SanitizedString<CustomSanitizer>", async () => {
        var value = new SanitizedString<CustomSanitizer>("secret");
        var copy = await client.EchoCustomSanitized(value);
        return copy.Value == "secret" && Sanitizer.Get<CustomSanitizer>().Apply("x") == "<custom>";
    });
    await Check("Option<int>", async ()
        => await client.EchoOption(Option.Some(5)) == Option.Some(5) && !(await client.EchoOption(default)).HasValue);
    await Check("int? (5)", async () => await client.EchoNullable(5) == 5);
    await Check("int? (null)", async () => await client.EchoNullable(null) is null);
    await Check("List<int>", async () => (await client.EchoList([1, 2])).SequenceEqual([1, 2]));
    var moment = Moment.Now;
    await Check("Moment?", async () => await client.EchoNullableMoment(moment) == moment);
    await Check("Moment[]", async () => (await client.EchoMomentArray([moment])).SequenceEqual([moment]));
    await Check("List<Moment>", async () => (await client.EchoMomentList([moment])).SequenceEqual([moment]));
    await Check("Dictionary<string, Moment>", async ()
        => (await client.EchoMomentMap(new() { ["a"] = moment }))["a"] == moment);
    await Check("ApiArray<int>", async ()
        => (await client.EchoApiArray(ApiArray.New(1, 2))).SequenceEqual([1, 2]));
    // PageRef<TKey> isn't [MemoryPackable]
    if (!formatKey.StartsWith("mempack", StringComparison.Ordinal))
        await Check("PageRef<long>", async () => await client.EchoPageRef(PageRef.New(10, 5L)) == PageRef.New(10, 5L));
    if (hasApiTypes) {
        await Check("ApiOption<int>", async ()
            => await client.EchoApiOption(ApiOption.Some(5)) == ApiOption.Some(5)
            && !(await client.EchoApiOption(default)).HasValue);
        await Check("ApiNullable<int>", async ()
            => (await client.EchoApiNullable(ApiNullable.Value(7))).Value == 7
            && (await client.EchoApiNullable(default)).Value is null);
        await Check("ApiNullable8<long>", async ()
            => (await client.EchoApiNullable8(ApiNullable8.Value(8L))).Value == 8L
            && (await client.EchoApiNullable8(default)).Value is null);
        await Check("ApiDto", async () => {
            var dto = new ApiDto(ApiOption.Some(1), ApiNullable.Value(2), ApiNullable8.Value(3L),
                PropertyBag<TypeSchema.Any>.Empty.Set("a", 4), new SanitizedString<Sanitizers.Hidden>("s"));
            var copy = await client.EchoDto(dto);
            return copy.Option == dto.Option
                && copy.Nullable.Value == 2
                && copy.Nullable8.Value == 3L
                && copy.Bag.Get<int>("a") == 4
                && copy.Secret.Value == "s";
        });
    }
    return failureCount;

    async Task Check(string name, Func<Task<bool>> check)
    {
        string result;
        try {
            result = await check.Invoke().WaitAsync(TimeSpan.FromSeconds(5)) ? "PASS" : "FAIL: wrong value";
        }
        catch (Exception e) {
            result = $"FAIL: {e.GetType().Name}: {e.Message}";
        }
        if (result != "PASS")
            failureCount++;
        WriteLine($"  {name}: {result}");
    }
}

public interface ITestService : IComputeService
{
    public Task<Moment> GetTime(CancellationToken cancellationToken = default);

    [ComputeMethod(AutoInvalidationDelay = 1)]
    public Task<Moment> GetTimeComputed(CancellationToken cancellationToken = default);

    [CommandHandler]
    public Task<string> OnSayHello(SayHelloCommand command, CancellationToken cancellationToken = default);

    public Task<PropertyBag<TypeSchema.Any>> EchoBag(
        PropertyBag<TypeSchema.Any> value, CancellationToken cancellationToken = default);
    public Task<PropertyBag<CustomSchema>> EchoCustomBag(
        PropertyBag<CustomSchema> value, CancellationToken cancellationToken = default);
    public Task<SanitizedString<Sanitizers.Hidden>> EchoSanitized(
        SanitizedString<Sanitizers.Hidden> value, CancellationToken cancellationToken = default);
    public Task<SanitizedString<CustomSanitizer>> EchoCustomSanitized(
        SanitizedString<CustomSanitizer> value, CancellationToken cancellationToken = default);
    public Task<Option<int>> EchoOption(Option<int> value, CancellationToken cancellationToken = default);
    public Task<int?> EchoNullable(int? value, CancellationToken cancellationToken = default);
    public Task<List<int>> EchoList(List<int> value, CancellationToken cancellationToken = default);
    public Task<Moment?> EchoNullableMoment(Moment? value, CancellationToken cancellationToken = default);
    public Task<Moment[]> EchoMomentArray(Moment[] value, CancellationToken cancellationToken = default);
    public Task<List<Moment>> EchoMomentList(List<Moment> value, CancellationToken cancellationToken = default);
    public Task<Dictionary<string, Moment>> EchoMomentMap(
        Dictionary<string, Moment> value, CancellationToken cancellationToken = default);
    public Task<ApiArray<int>> EchoApiArray(ApiArray<int> value, CancellationToken cancellationToken = default);
    public Task<PageRef<long>> EchoPageRef(PageRef<long> value, CancellationToken cancellationToken = default);
    public Task<ApiOption<int>> EchoApiOption(ApiOption<int> value, CancellationToken cancellationToken = default);
    public Task<ApiNullable<int>> EchoApiNullable(ApiNullable<int> value, CancellationToken cancellationToken = default);
    public Task<ApiNullable8<long>> EchoApiNullable8(
        ApiNullable8<long> value, CancellationToken cancellationToken = default);
    public Task<ApiDto> EchoDto(ApiDto value, CancellationToken cancellationToken = default);
}

[DataContract, MessagePackObject, MemoryPackable(GenerateType.VersionTolerant)]
[method: System.Text.Json.Serialization.JsonConstructor, SerializationConstructor, MemoryPackConstructor]
public sealed partial record SayHelloCommand(
    [property: DataMember(Order = 0), Key(0), MemoryPackOrder(0)] string Name
) : ICommand<string>;

[DataContract, MessagePackObject, MemoryPackable(GenerateType.VersionTolerant)]
[method: System.Text.Json.Serialization.JsonConstructor, SerializationConstructor, MemoryPackConstructor]
public sealed partial record ApiDto(
    [property: DataMember(Order = 0), Key(0), MemoryPackOrder(0)] ApiOption<int> Option,
    [property: DataMember(Order = 1), Key(1), MemoryPackOrder(1)] ApiNullable<int> Nullable,
    [property: DataMember(Order = 2), Key(2), MemoryPackOrder(2)] ApiNullable8<long> Nullable8,
    [property: DataMember(Order = 3), Key(3), MemoryPackOrder(3), MemoryPackAllowSerialize]
    PropertyBag<TypeSchema.Any> Bag,
    [property: DataMember(Order = 4), Key(4), MemoryPackOrder(4), MemoryPackAllowSerialize]
    SanitizedString<Sanitizers.Hidden> Secret);

public sealed class CustomSchema : TypeSchema
{
    // Keeps the parameterless constructor in trimmed apps
    static CustomSchema() => CodeKeeper.Keep<CustomSchema>();

    public override bool IsAllowed(Type type) => true;
}

public sealed class CustomSanitizer : Sanitizer
{
    // Keeps the parameterless constructor in trimmed apps
    static CustomSanitizer() => CodeKeeper.Keep<CustomSanitizer>();

    protected override string Sanitize(string value) => "<custom>";
}

public class TestService : ITestService
{
    public virtual Task<Moment> GetTime(CancellationToken cancellationToken = default)
        => Task.FromResult(Moment.Now);

    public virtual Task<Moment> GetTimeComputed(CancellationToken cancellationToken = default)
        => Task.FromResult(Moment.Now);

    public virtual Task<string> OnSayHello(SayHelloCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult($"Hello, {command.Name}");

    public virtual Task<PropertyBag<TypeSchema.Any>> EchoBag(
        PropertyBag<TypeSchema.Any> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<PropertyBag<CustomSchema>> EchoCustomBag(
        PropertyBag<CustomSchema> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<SanitizedString<Sanitizers.Hidden>> EchoSanitized(
        SanitizedString<Sanitizers.Hidden> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<SanitizedString<CustomSanitizer>> EchoCustomSanitized(
        SanitizedString<CustomSanitizer> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<Option<int>> EchoOption(Option<int> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<int?> EchoNullable(int? value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<List<int>> EchoList(List<int> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<Moment?> EchoNullableMoment(Moment? value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<Moment[]> EchoMomentArray(Moment[] value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<List<Moment>> EchoMomentList(List<Moment> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<Dictionary<string, Moment>> EchoMomentMap(
        Dictionary<string, Moment> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<ApiArray<int>> EchoApiArray(ApiArray<int> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<PageRef<long>> EchoPageRef(PageRef<long> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<ApiOption<int>> EchoApiOption(ApiOption<int> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<ApiNullable<int>> EchoApiNullable(
        ApiNullable<int> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<ApiNullable8<long>> EchoApiNullable8(
        ApiNullable8<long> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<ApiDto> EchoDto(ApiDto value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);
}

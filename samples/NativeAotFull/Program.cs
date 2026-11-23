using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using ActualLab.Api;
using ActualLab.Compliance;
using ActualLab.Fusion.Extensions;
using ActualLab.Rpc;
using ActualLab.Rpc.Compression;
using ActualLab.Trimming;
using MemoryPack;
using MessagePack;
using static System.Console;

// Calls a loopback RPC service with every wire format the serializer packages provide,
// round-tripping the types whose trimming / AOT support matters. Exit code 1 = a check failed.

// What each serializer needs under NativeAOT - one file per serializer in the Roots folder.
// ActualLab.Generators emits the MessagePack and System.Text.Json roots (ACTUALLAB_SERIALIZATION_ROOTS
// in the project file), so their Keep calls stay commented out; the rest have no generator support.
SerializableTypeRoots.Keep();
// MessagePackRoots.Keep();
// SystemJsonRoots.Keep();
SystemJsonRoots.Register();
MemoryPackRoots.Keep();
NerdbankRoots.Keep();
NerdbankRoots.Register();
NewtonsoftJsonRoots.Keep();

var formatKeys = args.Length > 0 ? args : GetAllFormatKeys();
var failureCount = 0;
foreach (var formatKey in formatKeys)
    failureCount += await TestFormat(formatKey);
WriteLine(failureCount == 0 ? "All checks passed." : $"{failureCount} check(s) failed.");
return failureCount == 0 ? 0 : 1;

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

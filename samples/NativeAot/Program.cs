using System.Reflection;
using ActualLab.Interception;
using ActualLab.Rpc;
using MemoryPack;
using MessagePack;
using static System.Console;

#pragma warning disable IL3050

AppDomain.CurrentDomain.UnhandledException += (_, args) => {
    Error.WriteLine($"UNHANDLED: {args.ExceptionObject}");
    Error.Flush();
};
AppDomain.CurrentDomain.FirstChanceException += (_, args) => {
    Error.WriteLine($"FCE: {args.Exception.GetType().Name}: {args.Exception.Message}");
    Error.Flush();
};

WriteLine($"RuntimeCodegen.Mode: {RuntimeCodegen.Mode}");
WriteLine($"ArgumentList.UseGenerics: {ArgumentList.UseGenerics}");
var l0 = ArgumentList.New();
var l2 = ArgumentList.New(1, "s");
var l10 = ArgumentList.New(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);
var m0 = typeof(Invoker).GetMethod(nameof(Invoker.Format0), BindingFlags.Public | BindingFlags.Static)!;
var m2 = typeof(Invoker).GetMethod(nameof(Invoker.Format2), BindingFlags.Public | BindingFlags.Static)!;
var m10 = typeof(Invoker).GetMethod(nameof(Invoker.Format10), BindingFlags.Public | BindingFlags.Static)!;
WriteLine(l0.GetInvoker(m0).Invoke(null, l0));
WriteLine(l2.GetInvoker(m2).Invoke(null, l2));
WriteLine(l10.GetInvoker(m10).Invoke(null, l10));

var services = new ServiceCollection()
    .AddLogging(l => {
        l.SetMinimumLevel(LogLevel.Debug);
        l.AddSimpleConsole();
    })
    .AddFusion(fusion => {
        fusion.Rpc.AddWebSocketClient();
        // .AddDistributedService can't be used w/ a router returning Loopback connection for it (see below)
        fusion.AddServerAndClient<ITestService, TestService>();
    })
    .AddSingleton(_ => RpcOutboundCallOptions.Default with {
        RouterFactory = methodDef => args => RpcPeerRef.Loopback,
    })
    .BuildServiceProvider();

var client = services.RpcHub().GetClient<ITestService>();
for (var i = 0; i < 5; i++) {
    Out.WriteLine("Calling GetTime()...");
    var now = await client.GetTime();
    Out.WriteLine($"GetTime() -> {now}");
    await TickSource.Default.WhenNextTick();
}
for (var i = 0; i < 5; i++) {
    var now = await client.GetTimeComputed();
    Out.WriteLine($"GetTimeComputed() -> {now}");
    await TickSource.Default.WhenNextTick();
}
var hello = await client.OnSayHello(new SayHelloCommand("AOT"));
Out.WriteLine($"OnSayHello() -> {hello}");

public static class Invoker
{
    public static string Format0()
        => "Format0";
    public static string Format2(int i, string s)
        => $"Format2: {i}, {s}";
    public static string Format10(int i0, int i1, int i2, int i3, int i4, int i5, int i6, int i7, int i8, int i9)
        => $"Format10: {i0}, {i1}, {i2}, {i3}, {i4}, {i5}, {i6}, {i7}, {i8}, {i9}";
}

public interface ITestService : IComputeService
{
    public Task<Moment> GetTime(CancellationToken cancellationToken = default);

    [ComputeMethod(AutoInvalidationDelay = 1)]
    public Task<Moment> GetTimeComputed(CancellationToken cancellationToken = default);

    [CommandHandler]
    public Task<string> OnSayHello(SayHelloCommand command, CancellationToken cancellationToken = default);
}

[MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
public sealed partial record SayHelloCommand(
    [property: MemoryPackOrder(0)] string Name
) : ICommand<string>;

public class TestService : ITestService
{
    public virtual Task<Moment> GetTime(CancellationToken cancellationToken = default)
        => Task.FromResult(Moment.Now);

    public virtual Task<Moment> GetTimeComputed(CancellationToken cancellationToken = default)
        => Task.FromResult(Moment.Now);

    public virtual Task<string> OnSayHello(SayHelloCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult($"Hello, {command.Name}");
}

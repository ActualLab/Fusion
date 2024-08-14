using System.Reflection;
using ActualLab.CommandR;
using ActualLab.CommandR.Configuration;
using ActualLab.DependencyInjection;
using ActualLab.Fusion;
using ActualLab.Interception;
using ActualLab.Reflection;
using ActualLab.Rpc;
using ActualLab.Time;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using static System.Console;

#pragma warning disable IL3050

WriteLine($"RuntimeCodegen.Mode: {RuntimeCodegen.Mode}");
var l0 = ArgumentList.New();
var l2 = ArgumentList.New(1, "s");
var m0 = typeof(Invoker).GetMethod(nameof(Invoker.Format0), BindingFlags.Public | BindingFlags.Static)!;
var m2 = typeof(Invoker).GetMethod(nameof(Invoker.Format2), BindingFlags.Public | BindingFlags.Static)!;
WriteLine(l0.GetInvoker(m0).Invoke(null, l0));
WriteLine(l2.GetInvoker(m2).Invoke(null, l2));

for (var i = 0; i < ArgumentList.Types.Length; i++) {
    var tArguments = Enumerable.Range(0, i).Select(_ => typeof(int)).ToArray();
    var t = ArgumentList.FindType(tArguments);
    var l = t.CreateInstance();
    var lengthGetter = t.GetProperty("Length")!.GetGetter();
    WriteLine($"{lengthGetter.Invoke(l)}: {l}, {FuncExt.GetFuncType(tArguments, typeof(object)).GetName()}, {FuncExt.GetActionType(tArguments).GetName()}");
}

var services = new ServiceCollection()
    .AddFusion(fusion => {
        fusion.AddComputeService<TestService>();
        fusion.AddClient<ITestService>(addCommandHandlers: false);
    })
    .AddRpc(rpc => {
        rpc.AddWebSocketClient();
        rpc.Service<ITestService>().HasServer(typeof(TestService));
    })
    .AddSingleton<RpcCallRouter>(_ => (method, args) => RpcPeerRef.Loopback)
    .BuildServiceProvider();

var client = services.GetRequiredService<ITestService>();
for (var i = 0; i < 5; i++) {
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

// Used types

public static class Invoker
{
    public static string Format0()
        => "Format0";
    public static string Format2(int a1, string a2)
        => $"Format2: {a1}, {a2}";
}

public interface ITestService : IComputeService
{
    Task<Moment> GetTime(CancellationToken cancellationToken = default);

    [ComputeMethod(AutoInvalidationDelay = 1)]
    Task<Moment> GetTimeComputed(CancellationToken cancellationToken = default);

    [CommandHandler]
    Task<string> OnSayHello(SayHelloCommand command, CancellationToken cancellationToken = default);
}

[MemoryPackable(GenerateType.VersionTolerant)]
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

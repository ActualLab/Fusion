﻿using System.Reflection;
using ActualLab.CommandR;
using ActualLab.CommandR.Configuration;
using ActualLab.DependencyInjection;
using ActualLab.Fusion;
using ActualLab.Generators;
using ActualLab.Interception;
using ActualLab.Mathematics;
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
var l10 = ArgumentList.New(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);
var m0 = typeof(Invoker).GetMethod(nameof(Invoker.Format0), BindingFlags.Public | BindingFlags.Static)!;
var m2 = typeof(Invoker).GetMethod(nameof(Invoker.Format2), BindingFlags.Public | BindingFlags.Static)!;
var m10 = typeof(Invoker).GetMethod(nameof(Invoker.Format10), BindingFlags.Public | BindingFlags.Static)!;
WriteLine(l0.GetInvoker(m0).Invoke(null, l0));
WriteLine(l2.GetInvoker(m2).Invoke(null, l2));
WriteLine(l10.GetInvoker(m10).Invoke(null, l10));

Type GetRandomType() => RandomShared.Next().PositiveModulo(5) switch {
    0 => typeof(bool),
    1 => typeof(int),
    2 => typeof(int?),
    3 => typeof(long),
    _ => typeof(string),
};
for (var i = 0; i < ArgumentList.NativeTypeCount + 2; i++) {
    var tArguments = Enumerable.Range(0, i).Select(_ => GetRandomType()).ToArray();
    var t = ArgumentList.GetListType(tArguments);
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
    public static string Format2(int i, string s)
        => $"Format2: {i}, {s}";
    public static string Format10(int i0, int i1, int i2, int i3, int i4, int i5, int i6, int i7, int i8, int i9)
        => $"Format2: {i0}, {i1}, {i2}, {i3}, {i4}, {i5}, {i6}, {i7}, {i8}, {i9}";
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

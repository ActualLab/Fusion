using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ActualLab.Api;
using ActualLab.Collections;
using ActualLab.Fusion.Authentication;
using ActualLab.Fusion.Blazor;
using ActualLab.Serialization.Internal;
using ActualLab.Trimming;
using MessagePack.Formatters;
using MessagePack.ImmutableCollection;
using Samples.TodoApp.UI;

/*
Console.WriteLine(CpuTimestamp.TickFrequency);
var s = CpuTimestamp.Now;
Enumerable.Range(0, 1000_0000).ToList();
Console.WriteLine(s.Elapsed.ToShortString());
*/

// Retain serialization code that full trimming can't trace because it's reached only
// via reflection. Never runs - the branch exists so the trimmer sees the references.
// MessagePack resolves collection formatters through reflection (DynamicGenericResolver /
// ImmutableCollectionResolver), so every such formatter the auth types use must be kept.
if (CodeKeeper.AlwaysFalse) {
    // Commands that return nothing complete with a System.Reactive.Unit result, whose
    // MessagePack formatter is resolved reflectively - keep its ctor under full trim.
    CodeKeeper.Keep<UnitMessagePackFormatter>();
    // MemoryPack's built-in PriorityQueue<,> formatter target, initialized at startup.
    CodeKeeper.Keep<PriorityQueue<object, object>>();
    // User.Claims / JsonCompatibleIdentities are ApiMap<string,string>.
    CodeKeeper.Keep<GenericDictionaryFormatter<string, string, ApiMap<string, string>>>();
    // The Authentication page lists sessions (ImmutableArray<SessionInfo>); SessionInfo.Options
    // is a typeless ImmutableOptionSet backed by ImmutableDictionary<string, object>. This sample
    // never stores option values, so no value-type formatters are needed.
    CodeKeeper.Keep<ImmutableArrayFormatter<SessionInfo>>();
    CodeKeeper.Keep<InterfaceImmutableDictionaryFormatter<string, object>>();
    CodeKeeper.KeepSerializable<SessionInfo>();
    CodeKeeper.KeepSerializable<ImmutableOptionSet>();
}

var culture = CultureInfo.CreateSpecificCulture("fr-FR");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

try {
    var builder = WebAssemblyHostBuilder.CreateDefault(args);
    ClientStartup.ConfigureServices(builder.Services, builder);
    var host = builder.Build();
    StaticLog.Factory = host.Services.LoggerFactory();
    ComponentInfo.DebugLog = StaticLog.For<ComponentInfo>();
    await host.RunAsync();
}
catch (Exception error) {
    // WASM surfaces only the outermost exception; log the whole chain so a boot
    // failure (e.g. a type trimmed away) is diagnosable from the browser console.
    for (var e = error; e != null; e = e.InnerException)
        Console.WriteLine($"{e.GetType().FullName}: {e.Message}\n{e.StackTrace}");
    throw;
}

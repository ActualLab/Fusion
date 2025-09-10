using System.Globalization;
using ActualLab.Fusion.Blazor;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Samples.TodoApp.UI;

/*
Console.WriteLine(CpuTimestamp.TickFrequency);
var s = CpuTimestamp.Now;
Enumerable.Range(0, 1000_0000).ToList();
Console.WriteLine(s.Elapsed.ToShortString());
*/

var culture = CultureInfo.CreateSpecificCulture("fr-FR");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
ClientStartup.ConfigureServices(builder.Services, builder);
var host = builder.Build();
StaticLog.Factory = host.Services.LoggerFactory();
ComponentInfo.DebugLog = StaticLog.For<ComponentInfo>();
await host.RunAsync();

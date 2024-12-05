using System.Globalization;
using ActualLab.DependencyInjection;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Samples.TodoApp.UI;

var culture = CultureInfo.CreateSpecificCulture("fr-FR");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
StartupHelper.ConfigureServices(builder.Services, builder);
var host = builder.Build();
StaticLog.Factory = host.Services.LoggerFactory();
await host.RunAsync();

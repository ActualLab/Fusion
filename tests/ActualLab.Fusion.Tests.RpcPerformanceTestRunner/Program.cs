using ActualLab.OS;
using ActualLab.Tests.Rpc;

#pragma warning disable MA0047, CA1050

WriteLine($".NET: {RuntimeInfo.DotNet.VersionString ?? RuntimeInformation.FrameworkDescription}");
await using var test = new RpcWebSocketTestWrapper(new ConsoleTestOutputHelper());
var serializationFormat = "nmsgpack6c";
await test.InitializeAsync();
await test.PerformanceTest(50_000, serializationFormat);
await test.ResetClientServices();
await test.PerformanceTest(200_000, serializationFormat);
await test.ResetClientServices();
await test.PerformanceTest(200_000, serializationFormat);
await test.ResetClientServices();
await test.PerformanceTest(200_000, serializationFormat);
await test.ResetClientServices();
await test.GetBytesTest(3, 5, 20_000);
WriteLine("Press any key to exit...");
ReadKey();

internal static class LogSettings
{
    public const LogLevel MinLogLevel = LogLevel.Error;
}

internal class RpcWebSocketTestWrapper(ITestOutputHelper @out) : RpcWebSocketPerformanceTest(@out)
{
    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureServices(services, isClient);

        // Add console logging with Debug level for all categories
        services.AddLogging(logging => {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Debug);
            // logging.AddFilter(logLevel => logLevel >= LogLevel.Debug);
            logging.AddFilter(logLevel => logLevel >= LogSettings.MinLogLevel);
        });
    }
}

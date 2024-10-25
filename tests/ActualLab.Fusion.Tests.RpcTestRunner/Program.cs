using ActualLab.OS;
using ActualLab.Tests.Rpc;

WriteLine($".NET: {RuntimeInfo.DotNet.VersionString ?? RuntimeInformation.FrameworkDescription}");
// await using var test = new RpcWebSocketPerformanceTest(new ConsoleTestOutputHelper());
// await test.InitializeAsync();
// await test.GetMemoryTest(10, 5, 20_000);
await using var test = new RpcWebSocketTest(new ConsoleTestOutputHelper());
await test.InitializeAsync();
await test.PerformanceTest(500_000, "mempack2c-np");
WriteLine("Press any key to exit...");
ReadKey();

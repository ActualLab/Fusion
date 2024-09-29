using ActualLab.Tests.Rpc;

var @out = new ConsoleTestOutputHelper();
// await using var test = new RpcWebSocketPerformanceTest(@out);
// await test.InitializeAsync();
// await test.GetMemoryTest(10, 5, 20_000);
await using var test = new RpcWebSocketTest(@out);
await test.InitializeAsync();
await test.PerformanceTest(1000_000, "mempack2");
WriteLine("Press any key to exit...");
ReadKey();

using ActualLab.Tests.Rpc;

var @out = new ConsoleTestOutputHelper();
await using var test = new RpcWebSocketPerformanceTest(@out);
await test.InitializeAsync();
await test.GetMemoryTest(10, 5, 20_000);
WriteLine("Press any key to exit...");
ReadKey();

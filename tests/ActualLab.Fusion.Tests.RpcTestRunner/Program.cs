using ActualLab.Tests.Rpc;

var @out = new ConsoleTestOutputHelper();
await using var test = new RpcWebSocketPerformanceTest(@out);
await test.InitializeAsync();
await test.GetMemoryTest(5, 500_000);
WriteLine("Press any key to exit...");
ReadKey();

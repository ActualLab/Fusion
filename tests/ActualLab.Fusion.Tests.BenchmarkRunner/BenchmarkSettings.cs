namespace ActualLab.Fusion.Tests.BenchmarkRunner;

public static class BenchmarkSettings
{
    public const int OperationCount = 100;
    public const int InvalidationBatchCount = 1024;
    public const int CodecOperationCount = 65_536;
    public const int DefaultProfileSeconds = 10;
    public const string RpcSerializationFormat = "msgpack6c";
}

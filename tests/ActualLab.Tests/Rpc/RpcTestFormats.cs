using ActualLab.Rpc;
using ActualLab.Rpc.Compression;

namespace ActualLab.Tests.Rpc;

/// <summary>
/// Wire format matrix sources for RPC theories: the full matrix on build agents
/// and full runs (ActualLab_FullTestRun=1), a reduced core set (one format per serializer
/// family + the "np" one) otherwise.
/// </summary>
public static class RpcTestFormats
{
    private static readonly bool UseFullMatrix = TestRunnerInfo.IsFullRun() || TestRunnerInfo.IsBuildAgent();
    private static readonly string[] FullMatrix = [
        "json5", "njson5", "json5np", "njson5np",
        "msgpack5", "msgpack5c", "msgpack6", "msgpack6c", "msgpack6k", "msgpack6ck",
#if !NETFRAMEWORK // MemoryPack doesn't run on .NET Framework
        "mempack5", "mempack5c", "mempack6", "mempack6c",
#endif
        "msgpack6c-lz4", "msgpack6c-lz4f",
#if NET8_0_OR_GREATER
        "nmsgpack6", "nmsgpack6c",
#endif
    ];
    private static readonly string[] CoreMatrix = [
        "json5", "njson5", "json5np", "msgpack6c", "msgpack6ck",
#if !NETFRAMEWORK
        "mempack6c",
#endif
#if NET8_0_OR_GREATER
        "nmsgpack6c",
#endif
    ];
    private static readonly string[] Matrix = UseFullMatrix ? FullMatrix : CoreMatrix;

    public static readonly TheoryData<string> All = NewData(Matrix);
    public static readonly TheoryData<string> NoNP = NewData(Matrix.Where(IsNotNP));
    public static readonly TheoryData<string, bool> NoNPWithReconnectFlag = NewReconnectFlagData();

    // Every format the tests use: the built-in ones, the format packages' ones, and the LZ4 variants
    // of the uncompressed v6 binary ones. Tests pass it to their resolvers explicitly.
    public static readonly ImmutableList<RpcWireFormat> Catalog = NewCatalog();

    // Lets a container built outside RpcTestBase serve every format of the Catalog
    public static RpcBuilder AddTestFormats(this RpcBuilder rpc)
    {
        rpc.ConfigureWireFormats(f => f.Add([.. Catalog]));
        return rpc;
    }

    // Private methods

    private static ImmutableList<RpcWireFormat> NewCatalog()
    {
        var formats = RpcDefaultWireFormats.All.ToList();
        formats.AddRange([RpcDefaultWireFormats.MessagePackV6K, RpcDefaultWireFormats.MessagePackV6CK]);
#if !NETFRAMEWORK
        formats.AddRange(RpcMemoryPackWireFormats.All);
#endif
        formats.AddRange(RpcNewtonsoftJsonWireFormats.All);
#if NET8_0_OR_GREATER
        formats.AddRange(RpcNerdbankWireFormats.All);
#endif
        var lz4Sources = formats
            .Where(x => x.DataFormat == DataFormat.Bytes && x.Key.Contains("6"))
            .ToList();
        foreach (var format in lz4Sources)
            formats.AddRange([format.WithLZ4("lz4"), format.WithLZ4("lz4f", compressionMode: RpcCompressionMode.Full)]);
        return formats.ToImmutableList();
    }

    private static bool IsNotNP(string format)
        => !format.EndsWith("np", StringComparison.Ordinal);

    private static TheoryData<string> NewData(IEnumerable<string> formats)
    {
        var data = new TheoryData<string>();
        foreach (var format in formats)
            data.Add(format);
        return data;
    }

    private static TheoryData<string, bool> NewReconnectFlagData()
    {
        // The compact formats get an extra allowReconnect: false case -
        // their stream serializers are the most complex ones.
        var data = new TheoryData<string, bool>();
        foreach (var format in Matrix.Where(IsNotNP)) {
            if (format is "mempack6c" or "msgpack6c")
                data.Add(format, false);
            data.Add(format, true);
        }
        return data;
    }
}

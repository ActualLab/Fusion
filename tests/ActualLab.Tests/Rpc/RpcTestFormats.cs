namespace ActualLab.Tests.Rpc;

/// <summary>
/// Serialization format matrix sources for RPC theories: the full matrix on build agents
/// and full runs (ActualLab_FullTestRun=1), a reduced core set (one format per serializer
/// family + the "np" one) otherwise.
/// </summary>
public static class RpcTestFormats
{
    private static readonly bool UseFullMatrix = TestRunnerInfo.IsFullRun() || TestRunnerInfo.IsBuildAgent();
    private static readonly string[] FullMatrix = [
        "json5", "njson5", "json5np", "njson5np",
        "mempack5", "mempack5c", "msgpack5", "msgpack5c",
        "mempack6", "mempack6c", "msgpack6", "msgpack6c",
#if NET8_0_OR_GREATER
        "nmsgpack6", "nmsgpack6c",
#endif
    ];
    private static readonly string[] CoreMatrix = [
        "json5", "njson5", "json5np", "mempack6c", "msgpack6c",
#if NET8_0_OR_GREATER
        "nmsgpack6c",
#endif
    ];
    private static readonly string[] Matrix = UseFullMatrix ? FullMatrix : CoreMatrix;

    public static readonly TheoryData<string> All = NewData(Matrix);
    public static readonly TheoryData<string> NoNP = NewData(Matrix.Where(IsNotNP));
    public static readonly TheoryData<string, bool> NoNPWithReconnectFlag = NewReconnectFlagData();

    // Private methods

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

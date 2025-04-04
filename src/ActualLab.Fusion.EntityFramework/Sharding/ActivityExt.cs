using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Fusion.EntityFramework;

public static class ActivityExt
{
#if !NETSTANDARD2_0
    [return: NotNullIfNotNull("activity")]
#endif
    public static Activity? AddShardTags(this Activity? activity, string shard)
        => activity?.AddTag("shard", shard);
}

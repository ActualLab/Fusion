using System.Diagnostics;

namespace ActualLab.Fusion.EntityFramework;

/// <summary>
/// Extension methods for <see cref="Activity"/> to add shard-related tags.
/// </summary>
public static class ActivityExt
{
#if !NETSTANDARD2_0
    [return: NotNullIfNotNull("activity")]
#endif
    public static Activity? AddShardTags(this Activity? activity, string shard)
        => activity?.AddTag("shard", shard);
}

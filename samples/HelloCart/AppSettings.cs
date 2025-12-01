using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Resilience;

namespace Samples.HelloCart;

public static class AppSettings
{
    public static readonly bool UseAutoRunner = true;
    public static readonly bool EnableRandomLogMessageCommandFailures = false;

    public static class Db
    {
        public static readonly bool UsePostgreSql = true;
        public static readonly bool UseOperationLogWatchers = true;
        public static readonly bool UseRedisOperationLogWatchers = true;
        public static readonly bool UseOperationReprocessor = true;

        public static readonly bool UseChaosMaker = true;
        public static readonly ChaosMaker ChaosMaker = (
            (0.1*ChaosMaker.Delay(0.75, 1)) |
            (0.1*ChaosMaker.TransientError)
        ).Filtered("Operations Framework types", o => o is DbOperationScope or IDbLogReader).Gated();
    }
}

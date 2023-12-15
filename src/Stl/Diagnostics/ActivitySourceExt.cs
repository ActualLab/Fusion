using System.Diagnostics;

namespace ActualLab.Diagnostics;

public static class ActivitySourceExt
{
    public static readonly ActivitySource Unknown = new("<Unknown>");

    public static Activity? StartActivity(
        this ActivitySource activitySource,
        Type sourceType,
        string operationName,
        ActivityKind activityKind = ActivityKind.Internal)
        => activitySource.StartActivity(
            sourceType.GetOperationName(operationName),
            activityKind);
}

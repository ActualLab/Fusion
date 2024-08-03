using System.Diagnostics;

namespace ActualLab.Diagnostics;

public static class ActivitySourceExt
{
    public static ActivitySource? IfEnabled(this ActivitySource activitySource, bool isEnabled)
        => isEnabled ? activitySource : null;

    public static Activity? StartActivity(
        this ActivitySource? activitySource,
        Type sourceType,
        [CallerMemberName] string operationName = "",
        ActivityKind activityKind = ActivityKind.Internal)
        => activitySource?.StartActivity(
            sourceType.GetOperationName(operationName),
            activityKind);
}

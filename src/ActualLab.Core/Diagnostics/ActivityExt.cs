using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Diagnostics;

public static class ActivityExt
{
    [return: NotNullIfNotNull(nameof(activity))]
    public static Activity MaybeSetError(this Activity activity, Task errorSource)
    {
        try {
            var error = errorSource.ToResultSynchronously().Error;
            return activity.MaybeSetError(error, detectCancellation: true);
        }
        catch (Exception e) {
            // The task is not completed yet,
            StaticLog.For(typeof(ActivatorExt)).LogError(e, "The provided task isn't completed yet");
            return activity;
        }
    }

    [return: NotNullIfNotNull(nameof(activity))]
    public static Activity MaybeSetError(this Activity activity, Task errorSource, CancellationToken cancellationToken)
    {
        try {
            var error = errorSource.ToResultSynchronously().Error;
            return activity.MaybeSetError(error, cancellationToken);
        }
        catch (Exception e) {
            // The task is not completed yet,
            StaticLog.For(typeof(ActivatorExt)).LogError(e, "The provided task isn't completed yet");
            return activity;
        }
    }

    [return: NotNullIfNotNull(nameof(activity))]
    public static Activity MaybeSetError(this Activity activity, Exception? error, CancellationToken cancellationToken)
    {
        if (error == null)
            return activity;

        var description = error.IsCancellationOf(cancellationToken)
            ? "Cancelled"
            : $"{error.GetType().GetName()}: {error.Message}";
        activity.SetStatus(ActivityStatusCode.Error, description);
        return activity;
    }

    [return: NotNullIfNotNull(nameof(activity))]
    public static Activity MaybeSetError(this Activity activity, Exception? error, bool detectCancellation = false)
    {
        if (error == null)
            return activity;

        var description = detectCancellation && error is OperationCanceledException
            ? "Cancelled"
            : $"{error.GetType().GetName()}: {error.Message}";
        activity.SetStatus(ActivityStatusCode.Error, description);
        return activity;
    }

}

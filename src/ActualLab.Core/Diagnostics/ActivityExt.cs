using System.Diagnostics;

namespace ActualLab.Diagnostics;

public static class ActivityExt
{
    public static Activity Finalize(this Activity activity, Task errorSource)
    {
        try {
            var error = errorSource.ToResultSynchronously().Error;
            return activity.Finalize(error, detectCancellation: true);
        }
        catch (Exception e) {
            // The task is not completed yet,
            StaticLog.For(typeof(ActivatorExt)).LogError(e, "The provided task isn't completed yet");
            return activity;
        }
    }

    public static Activity Finalize(this Activity activity, Task errorSource, CancellationToken cancellationToken)
    {
        try {
            var error = errorSource.ToResultSynchronously().Error;
            return activity.Finalize(error, cancellationToken);
        }
        catch (Exception e) {
            // The task is not completed yet,
            StaticLog.For(typeof(ActivityExt)).LogError(e, "The provided task isn't completed yet");
            return activity;
        }
    }

    public static Activity Finalize(this Activity activity, Exception? error, CancellationToken cancellationToken)
    {
        if (error == null)
            return activity;

        if (error.IsCancellationOf(cancellationToken)) {
            activity.SetStatus(ActivityStatusCode.Ok, "Cancelled");
            return activity;
        }

        var description = $"{error.GetType().GetName()}: {error.Message}";
        activity.SetStatus(ActivityStatusCode.Error, description);
        return activity;
    }

    public static Activity Finalize(this Activity activity, Exception? error, bool detectCancellation = false)
    {
        if (error == null)
            return activity;

        if (detectCancellation && error is OperationCanceledException) {
            activity.SetStatus(ActivityStatusCode.Ok, "Cancelled");
            return activity;
        }

        var description = $"{error.GetType().GetName()}: {error.Message}";
        activity.SetStatus(ActivityStatusCode.Error, description);
        return activity;
    }

}

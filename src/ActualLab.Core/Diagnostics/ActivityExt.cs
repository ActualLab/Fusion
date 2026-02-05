using System.Diagnostics;
using ActualLab.Rpc;

namespace ActualLab.Diagnostics;

/// <summary>
/// Extension methods for <see cref="Activity"/> to finalize activities
/// with error status and handle disposal safely.
/// </summary>
public static class ActivityExt
{
    // ReSharper disable once SuspiciousTypeConversion.Global
    public static Func<Exception, bool> IsError { get; set; }
        = static e => e is not INotAnError;

    // Finalize

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
        if (error is null || !IsError.Invoke(error))
            return activity;

        if (error is RpcRerouteException) {
            activity.SetStatus(ActivityStatusCode.Ok, "Rerouted");
            return activity;
        }
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
        if (error is null || !IsError.Invoke(error))
            return activity;

        if (error is RpcRerouteException) {
            activity.SetStatus(ActivityStatusCode.Ok, "Rerouted");
            return activity;
        }
        if (detectCancellation && error is OperationCanceledException) {
            activity.SetStatus(ActivityStatusCode.Ok, "Cancelled");
            return activity;
        }

        var description = $"{error.GetType().GetName()}: {error.Message}";
        activity.SetStatus(ActivityStatusCode.Error, description);
        return activity;
    }

    // DisposeSafely

    public static void DisposeNonCurrent(this Activity activity)
    {
        var oldCurrentActivity = Activity.Current;
        activity.Dispose();
        if (!ReferenceEquals(oldCurrentActivity, activity)) {
            // Another activity was current, so we restore it
            try {
                Activity.Current = oldCurrentActivity;
            }
            catch {
                // Intended
            }
        }
    }
}

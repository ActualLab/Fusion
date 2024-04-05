using System.Runtime.ExceptionServices;

namespace ActualLab;

/// <summary>
/// Extension methods for <see cref="Exception"/> type and its descendants.
/// </summary>
public static class ExceptionExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCancellationOf(this Exception error, CancellationToken cancellationToken)
        => error is OperationCanceledException && cancellationToken.IsCancellationRequested;

    // Flatten, Any, GetFirstInnerException

    public static IReadOnlyList<Exception> Flatten(this Exception? exception)
    {
        if (exception == null)
            return Array.Empty<Exception>();

        var result = new List<Exception>();
        Traverse(result, exception);
        return result;

        void Traverse(List<Exception> list, Exception ex) {
            if (ex is AggregateException ae) {
                foreach (var e in ae.InnerExceptions)
                    if (e != null!)
                        Traverse(list, e);
            }
            if (ex.InnerException is { } ie)
                Traverse(list, ie);
            list.Add(ex);
        }
    }

    public static bool Any(this Exception? exception, Func<Exception, bool> predicate)
    {
        if (exception == null)
            return false;
        if (predicate.Invoke(exception))
            return true;

        if (exception is AggregateException ae) {
            foreach (var e in ae.InnerExceptions)
                if (Any(e, predicate))
                    return true;
        }
        return exception.InnerException is { } ie && Any(ie, predicate);
    }

    public static Exception GetFirstInnerException(this AggregateException exception)
    {
        while (exception.InnerExceptions.Count > 0) {
            var e = exception.InnerExceptions[0];
            if (e is AggregateException ae)
                exception = ae;
            else
                return e;
        }
        return exception;
    }

    // LogXxx

    public static Exception LogError(this Exception error, ILogger? log)
        => error.LogError(log, error);
    public static T LogError<T>(this Exception error, ILogger? log, T replacement)
    {
#if NET5_0_OR_GREATER
        ExceptionDispatchInfo.SetCurrentStackTrace(error);
#endif
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        log?.LogError(error, error.Message);
        return replacement;
    }

    public static Exception LogWarning(this Exception error, ILogger? log)
        => error.LogWarning(log, error);
    public static T LogWarning<T>(this Exception error, ILogger? log, T result)
    {
#if NET5_0_OR_GREATER
        ExceptionDispatchInfo.SetCurrentStackTrace(error);
#endif
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        log?.LogWarning(error, error.Message);
        return result;
    }
}

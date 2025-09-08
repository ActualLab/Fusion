namespace ActualLab.Resilience;

public record RetryLogger(ILogger? Log, [CallerMemberName] string Action = "(unknown)")
{
    public void LogError(Exception error, int failedTryCount, string reason)
        => LogError(error, failedTryCount, null, reason);

    public void LogError(Exception error, int failedTryCount, int? tryCount, string reason)
    {
        if (Log?.IsEnabled(LogLevel.Error) != true)
            return;

        if (tryCount.HasValue)
            Log.LogWarning(error,
                // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                "{Action} failed (#{FailedTryCount}/{TryCount}) - {Reason}",
                Action, failedTryCount, tryCount.GetValueOrDefault(), reason);
        else
            Log.LogWarning(error,
                // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                "{Action} failed (#{FailedTryCount}) - {Reason}",
                Action, failedTryCount, reason);
    }

    public void LogRetry(Exception error, int failedTryCount, TimeSpan retryDelay)
        => LogRetry(error, failedTryCount, null, retryDelay);

    public void LogRetry(Exception error, int failedTryCount, int? tryCount, TimeSpan retryDelay)
    {
        if (Log?.IsEnabled(LogLevel.Warning) != true)
            return;

        var delayPart = retryDelay > TimeSpan.Zero ? $" in {retryDelay.ToShortString()}" : "";
        if (tryCount.HasValue)
            Log.LogWarning(error,
                // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                "{Action} failed (#{FailedTryCount}/{TryCount}), will retry" + delayPart,
                Action, failedTryCount, tryCount.GetValueOrDefault());
        else
            Log.LogWarning(error,
                // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                "{Action} failed (#{FailedTryCount}), will retry" + delayPart,
                Action, failedTryCount);
    }
}

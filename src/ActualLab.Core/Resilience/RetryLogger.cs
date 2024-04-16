namespace ActualLab.Resilience;

public record RetryLogger(string Action, ILogger? Log)
{
    public void LogError(Exception error, int failedTryCount, int tryCount, string reason)
    {
        if (Log?.IsEnabled(LogLevel.Error) != true)
            return;

        Log.LogWarning(error,
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            "{Action} failed (#{FailedTryCount}/{TryCount}) - {Reason}",
            Action, failedTryCount, tryCount, reason);
    }

    public void LogRetry(Exception error, int failedTryCount, int tryCount, TimeSpan retryDelay)
    {
        if (Log?.IsEnabled(LogLevel.Warning) != true)
            return;

        var delayPart = retryDelay > TimeSpan.Zero ? $" in {retryDelay.ToShortString()}" : "";
        Log.LogWarning(error,
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            "{Action} failed (#{FailedTryCount}/{TryCount}), will retry" + delayPart,
            Action, failedTryCount, tryCount);
    }

    public void LogRetry(Exception error, int failedTryCount, TimeSpan retryDelay)
    {
        if (Log?.IsEnabled(LogLevel.Warning) != true)
            return;

        var delayPart = retryDelay > TimeSpan.Zero ? $" in {retryDelay.ToShortString()}" : "";
        Log.LogWarning(error,
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            "{Action} failed (#{FailedTryCount}), will retry" + delayPart,
            Action, failedTryCount);
    }
}

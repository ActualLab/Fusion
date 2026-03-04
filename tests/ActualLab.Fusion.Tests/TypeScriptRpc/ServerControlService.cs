namespace ActualLab.Fusion.Tests.TypeScriptRpc;

public class ServerControlService : IServerControlService
{
    private static TaskCompletionSource<Unit> _restartTcs = TaskCompletionSourceExt.New<Unit>();

    public static Task WhenRestartRequested => _restartTcs.Task;

    public static void Reset() => _restartTcs = TaskCompletionSourceExt.New<Unit>();

    public Task RequestRestart()
    {
        _restartTcs.TrySetResult(default);
        return Task.CompletedTask;
    }
}

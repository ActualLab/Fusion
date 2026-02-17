namespace ActualLab.Fusion.Tests.TypeScriptRpc;

public class ServerControlService : IServerControlService
{
    private static TaskCompletionSource _restartTcs = new();

    public static Task WhenRestartRequested => _restartTcs.Task;

    public static void Reset() => _restartTcs = new TaskCompletionSource();

    public Task RequestRestart()
    {
        _restartTcs.TrySetResult();
        return Task.CompletedTask;
    }
}

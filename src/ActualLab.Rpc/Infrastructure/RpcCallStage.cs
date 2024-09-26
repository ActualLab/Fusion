namespace ActualLab.Rpc.Infrastructure;

public static class RpcCallStage
{
    private static readonly Lock Lock = new();
    private static Dictionary<int, string> _callStageNames = new() {
        { ResultReady, nameof(ResultReady) },
        { ResultReady | Unregistered, "*" + nameof(ResultReady) },
        { Invalidated, nameof(Invalidated) },
        { Invalidated | Unregistered, "*" + nameof(Invalidated) },
        { Unregistered, "*" + nameof(Unregistered) },
    };

    public const int ResultReady = 1;
    public const int Invalidated = 3;
    public const int Unregistered = 0x1_000;

    public static void Register(int value, string name)
    {
        lock (Lock) {
            var callStageNames = new Dictionary<int, string>(_callStageNames) {
                { value, name },
                { value | Unregistered, "*" + name },
            };
            _callStageNames = callStageNames;
        }
    }

    public static string GetName(int completedStage)
        => _callStageNames.GetValueOrDefault(completedStage) ?? "";
}

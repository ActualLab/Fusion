using ActualLab.OS;

namespace ActualLab.Rpc;

public static class RpcSettings
{
    private static readonly object Lock = new();
    private static RpcMode _mode;

    public static RpcMode Mode {
        get => _mode;
        set {
            if (value is not (RpcMode.Client or RpcMode.Server))
                throw new ArgumentOutOfRangeException(nameof(value), value, null);

            lock (Lock) {
                _mode = value;
                Recompute();
            }
        }
    }

    public static Func<Task>? WebSocketWriteDelayFactory { get; set; }

    static RpcSettings()
        => Mode = OSInfo.IsAnyClient ? RpcMode.Client : RpcMode.Server;

    private static void Recompute()
    {
        var isServer = Mode is RpcMode.Server;
        WebSocketWriteDelayFactory = isServer
            ? null
            : TaskExt.YieldDelay;
    }
}

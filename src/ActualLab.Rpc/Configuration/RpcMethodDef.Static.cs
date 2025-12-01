namespace ActualLab.Rpc;

public partial class RpcMethodDef
{
    // Static helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ComposeFullName(string serviceName, string methodName)
        => string.Concat(serviceName, ".", methodName);

    public static (string ServiceName, string MethodName) SplitFullName(string fullName)
    {
        var dotIndex = fullName.LastIndexOf('.');
        if (dotIndex < 0)
            return ("", fullName);

        var serviceName = fullName[..dotIndex];
        var methodName = fullName[(dotIndex + 1)..];
        return (serviceName, methodName);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "We assume RPC-related code is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "We assume RPC-related code is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2072",
        Justification = "We assume RPC-related code is fully preserved")]
    public static bool IsCommandType(Type type, out bool isBackendCommand)
    {
        (var isCommand, isBackendCommand) = IsCommandTypeCache.GetOrAdd(
            type,
            static t => {
                var interfaces = t.GetInterfaces();
                var isCommand =
                    interfaces.Any(x => CommandInterfaceFullName.Equals(x.FullName, StringComparison.Ordinal));
                var isBackendCommand = isCommand
                    && interfaces.Any(x =>
                        BackendCommandInterfaceFullName.Equals(x.FullName, StringComparison.Ordinal));
                return (isCommand, isBackendCommand);
            });
        return isCommand;
    }
}

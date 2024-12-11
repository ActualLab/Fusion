using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Internal;
using ActualLab.OS;

namespace ActualLab.CommandR;

public static class CommandExt
{
    private static readonly ConcurrentDictionary<Type, Type> ResultTypeCache = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<(Type, Symbol, string, string), string> OperationNameCache = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly Type CommandWithResultType = typeof(ICommand<>);

    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume all command handling code is preserved")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Type GetResultType(this ICommand command)
        => GetResultType(command.GetType());

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "We assume all command handling code is preserved")]
    public static Type GetResultType(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type commandType)
    {
        if (commandType == null)
            throw new ArgumentNullException(nameof(commandType));

        var result = ResultTypeCache.GetOrAdd(commandType, static tCommand => {
            foreach (var tInterface in tCommand.GetInterfaces()) {
                if (!tInterface.IsConstructedGenericType)
                    continue;
                var gInterface = tInterface.GetGenericTypeDefinition();
                if (gInterface != CommandWithResultType)
                    continue;

                return tInterface.GetGenericArguments()[0];
            }
            return null!;
        });
        return result ?? throw Errors.CommandMustImplementICommandOfTResult(commandType);
    }

    public static string GetOperationName(this ICommand command, string operation = "", string prefix = "cmd")
    {
        var type = command.GetType();
        var chainId = command is IEventCommand eventCommand
            ? eventCommand.ChainId
            : default;
        return OperationNameCache.GetOrAdd((type, chainId, operation, prefix),
            static key => {
                var (type, chainId, operation, prefix) = key;
                var result = prefix + "." + DiagnosticsExt.FixName(type.NonProxyType().GetName());
                if (!chainId.IsEmpty)
                    result += $"-{DiagnosticsExt.FixName(chainId.Value)}";
                if (!operation.IsNullOrEmpty())
                    result += $".{operation}";
                return result;
            });
    }
}

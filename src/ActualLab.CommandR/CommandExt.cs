using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Internal;

namespace ActualLab.CommandR;

public static class CommandExt
{
    private static readonly ConcurrentDictionary<Type, Type> ResultTypeCache = new();
    private static readonly ConcurrentDictionary<(Type, Symbol, string), string> OperationNameCache = new();
    private static readonly Type CommandWithResultType = typeof(ICommand<>);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Type GetResultType(this ICommand command)
        => GetResultType(command.GetType());

    public static Type GetResultType(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type commandType)
    {
        if (commandType == null)
            throw new ArgumentNullException(nameof(commandType));

        var result = ResultTypeCache.GetOrAdd(commandType, static tCommand => {
#pragma warning disable IL2070
            foreach (var tInterface in tCommand.GetInterfaces()) {
#pragma warning restore IL2070
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

    public static string GetOperationName(this ICommand command, string operation = "")
    {
        var type = command.GetType();
        var chainId = command is IEventCommand eventCommand
            ? eventCommand.ChainId
            : default;
        return OperationNameCache.GetOrAdd((type, chainId, operation),
            static key => {
                var (type, chainId, operation) = key;
                var result = "command." + type.NonProxyType().GetName();
                if (!chainId.IsEmpty)
                    result += $"-{chainId.Value}";
                if (!operation.IsNullOrEmpty())
                    result += $".{operation}";
                return result;
            });
    }
}

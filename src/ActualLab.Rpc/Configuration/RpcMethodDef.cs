using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.OS;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc;

public sealed class RpcMethodDef : MethodDef
{
    public static string CommandInterfaceFullName { get; set; } = "ActualLab.CommandR.ICommand";
    public static string BackendCommandInterfaceFullName { get; set; } = "ActualLab.CommandR.IBackendCommand";

    private static readonly HashSet<string> StreamingMethodNames
        = new(StringComparer.Ordinal) { "Ack", "AckEnd", "B", "I", "End" };
    private static readonly ConcurrentDictionary<Type, (bool, bool)> IsCommandTypeCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public RpcHub Hub { get; }
    public RpcServiceDef Service { get; }

    public string Name {
        get;
        init {
            if (value.IsNullOrEmpty())
                throw new ArgumentOutOfRangeException(nameof(value));

            field = value;
            FullName = ComposeFullName(Service.Name, value);
            Ref = new RpcMethodRef(FullName, this);
        }
    }

    public new readonly string FullName = "";
    public readonly RpcMethodRef Ref;

    [field: AllowNull, MaybeNull] // Lazy: costly to construct in advance (delegate creation, etc.)
    public ArgumentListType ArgumentListType => field ??= ArgumentListType.Get(ParameterTypes);
    [field: AllowNull, MaybeNull] // Lazy: costly to construct in advance (delegate creation, etc.)
    public ArgumentListType ResultListType => field ??= ArgumentListType.Get(UnwrappedReturnType);
    public readonly bool NoWait;
    public readonly bool HasPolymorphicArguments;
    public readonly bool HasPolymorphicResult;
    public readonly bool IsSystem;
    public readonly bool IsBackend;
    public RpcMethodKind Kind { get; init; }
    public RpcSystemMethodKind SystemMethodKind { get; init; }
    public LegacyNames LegacyNames { get; init; } = LegacyNames.Empty;
    public RpcCallTimeouts Timeouts { get; init; } = RpcCallTimeouts.None;
    public Action<RpcInboundCall>? CallValidator { get; init; }
    public RpcCallTracer? Tracer { get; init; }
    public PropertyBag Properties { get; init; }

    public RpcMethodDef(
        RpcServiceDef service,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        MethodInfo method
        ) : base(serviceType, method)
    {
        if (serviceType != service.Type)
            throw new ArgumentOutOfRangeException(nameof(serviceType));

        Service = service;
        Hub = service.Hub;
        NoWait = UnwrappedReturnType == typeof(RpcNoWait);
        var nameSuffix = $":{ParameterTypes.Length}";
        Name = Method.Name + nameSuffix;
        HasPolymorphicArguments = ParameterTypes.Any(RpcArgumentSerializer.IsPolymorphic);
        HasPolymorphicResult = RpcArgumentSerializer.IsPolymorphic(UnwrappedReturnType);

        if (!IsAsyncMethod) { // Invalid method
            IsValid = false;
            return;
        }

        if (service.IsSystem) { // System method
            IsSystem = true;
            Kind = RpcMethodKind.System;
            SystemMethodKind = service.Type == typeof(IRpcSystemCalls)
                ? Method.Name switch {
                    nameof(IRpcSystemCalls.Ok) => RpcSystemMethodKind.Ok,
                    nameof(IRpcSystemCalls.I) => RpcSystemMethodKind.Item,
                    nameof(IRpcSystemCalls.B) => RpcSystemMethodKind.Batch,
                    _ => StreamingMethodNames.Contains(method.Name)
                        ? RpcSystemMethodKind.OtherStreaming
                        : RpcSystemMethodKind.OtherNonStreaming,
                }
                : RpcSystemMethodKind.OtherNonStreaming;
            return;
        }

        // Non-system method
        Kind = GetKind(this, out var isBackend);
        IsBackend = service.IsBackend || isBackend;
        LegacyNames = new LegacyNames(Method, nameSuffix);
        Timeouts = Hub.CallTimeoutsProvider.Invoke(this).Normalize();
        CallValidator = Hub.CallValidatorProvider.Invoke(this);
        Tracer = Hub.CallTracerFactory.Invoke(this);
    }

    public override string ToString()
        => _toStringCached ??= ToString(useShortName: false);

    public string ToString(bool useShortName)
    {
        var arguments = ParameterTypes.Select(t => t.GetName()).ToDelimitedString();
        var returnType = UnwrappedReturnType.GetName();
        return  $"'{(useShortName ? Name : FullName)}': ({arguments}) -> {returnType}{(IsValid ? "" : " - invalid")}";
    }

    // Helpers

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

    public bool IsCallResultMethod()
    {
        if (!IsSystem)
            return false;

        var systemCallSender = Hub.SystemCallSender;
        return this == systemCallSender.OkMethodDef
            || this == systemCallSender.ErrorMethodDef;
    }

    public static RpcMethodKind GetKind(RpcMethodDef method, out bool isBackend)
    {
        var parameterTypes = method.ParameterTypes;
        if (parameterTypes.Length == 2 && parameterTypes[1] == typeof(CancellationToken))
            return IsCommandType(parameterTypes[0], out isBackend)
                ? RpcMethodKind.Command
                : RpcMethodKind.Query;

        isBackend = false;
        return RpcMethodKind.Query;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume RPC-related code is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "We assume RPC-related code is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume RPC-related code is fully preserved")]
    public static bool IsCommandType(Type type, out bool isBackendCommand)
    {
        (var isCommand, isBackendCommand) = IsCommandTypeCache.GetOrAdd(
            type,
            static t => {
                var interfaces = t.GetInterfaces();
                var isCommand = interfaces.Any(x => CommandInterfaceFullName.Equals(x.FullName, StringComparison.Ordinal));
                var isBackendCommand = isCommand && interfaces.Any(x => BackendCommandInterfaceFullName.Equals(x.FullName, StringComparison.Ordinal));
                return (isCommand, isBackendCommand);
            });
        return isCommand;
    }
}

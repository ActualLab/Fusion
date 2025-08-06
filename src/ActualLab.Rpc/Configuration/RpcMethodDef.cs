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

    private static readonly HashSet<string> StreamMethodNames
        = new(StringComparer.Ordinal) { "Ack", "AckEnd", "B", "I", "End" };
    private static readonly ConcurrentDictionary<Type, (bool, bool)> IsCommandTypeCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    private string? _toStringCached;

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
    public readonly bool IsSystem;
    public readonly bool IsBackend;
    public readonly bool IsStream;
    public readonly bool HasPolymorphicArguments;
    public readonly bool HasPolymorphicResult;
    public bool IsCommand { get; init; }
    public RpcCallTracer? Tracer { get; init; }
    public LegacyNames LegacyNames { get; init; }
    public RpcCallTimeouts Timeouts { get; init; }
    public Action<RpcInboundCall>? CallValidator { get; init; }
    public RpcSystemCallKind SystemCallKind { get; init; }
    public PropertyBag Properties { get; init; }

    public RpcMethodDef(
        RpcServiceDef service,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        MethodInfo method
        ) : base(serviceType, method)
    {
        if (serviceType != service.Type)
            throw new ArgumentOutOfRangeException(nameof(serviceType));

        Hub = service.Hub;
        NoWait = UnwrappedReturnType == typeof(RpcNoWait);
        IsSystem = service.IsSystem;
        IsStream = IsSystem && StreamMethodNames.Contains(method.Name);
        IsCommand = false;
        IsBackend = service.IsBackend;
        if (ParameterTypes.Length == 2 && ParameterTypes[1] == typeof(CancellationToken)) {
            IsCommand = IsCommandType(ParameterTypes[0], out bool isBackendCommand);
            IsBackend |= isBackendCommand;
        }
        HasPolymorphicArguments = ParameterTypes.Any(RpcArgumentSerializer.IsPolymorphic);
        HasPolymorphicResult = RpcArgumentSerializer.IsPolymorphic(UnwrappedReturnType);

        Service = service;
        var nameSuffix = $":{ParameterTypes.Length}";
        Name = Method.Name + nameSuffix;

        if (!IsAsyncMethod)
            IsValid = false;

        Tracer = Hub.CallTracerFactory.Invoke(this);
        LegacyNames = new LegacyNames(Method
            .GetCustomAttributes<LegacyNameAttribute>(false)
            .Select(x => LegacyName.New(x, nameSuffix)));

        Timeouts = Hub.CallTimeoutsProvider.Invoke(this).Normalize();
        CallValidator = Hub.CallValidatorProvider.Invoke(this);

        SystemCallKind = service.Type == typeof(IRpcSystemCalls)
            ? Method.Name switch {
                nameof(IRpcSystemCalls.Ok) => RpcSystemCallKind.Ok,
                nameof(IRpcSystemCalls.I) => RpcSystemCallKind.Item,
                nameof(IRpcSystemCalls.B) => RpcSystemCallKind.Batch,
                _ => RpcSystemCallKind.OtherOrNone,
            }
            : RpcSystemCallKind.OtherOrNone;
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

    public bool IsStreamResultMethod()
    {
        var systemCallSender = Hub.SystemCallSender;
        return this == systemCallSender.BatchMethodDef
            || this == systemCallSender.ItemMethodDef
            || this == systemCallSender.EndMethodDef;
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

using System.Diagnostics.CodeAnalysis;
using ActualLab.Caching;
using ActualLab.Interception;
using ActualLab.OS;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc;

#pragma warning disable CA2214 // Do not call overridable methods in constructors

public partial class RpcMethodDef : MethodDef
{
    public static string CommandInterfaceFullName { get; set; } = "ActualLab.CommandR.ICommand";
    public static string BackendCommandInterfaceFullName { get; set; } = "ActualLab.CommandR.IBackendCommand";

    private static readonly HashSet<string> StreamingMethodNames
        = new(StringComparer.Ordinal) { "Ack", "AckEnd", "B", "I", "End" };
    private static readonly ConcurrentDictionary<Type, (bool, bool)> IsCommandTypeCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    [field: AllowNull, MaybeNull]
    protected ILogger Log => field ??= Hub.Services.LogFor(GetType());

    public readonly RpcHub Hub;
    public readonly RpcServiceDef Service;

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

    public readonly bool NoWait;
    public readonly bool HasPolymorphicArguments;
    public readonly bool HasPolymorphicResult;
    public bool IsSystem => Kind is RpcMethodKind.System;
    public readonly bool IsBackend;
    public RpcMethodKind Kind { get; init; }
    public RpcSystemMethodKind SystemMethodKind { get; init; }
    public LegacyNames LegacyNames { get; init; } = LegacyNames.Empty;
    public PropertyBag Properties { get; init; }
    public RpcCallTracer? Tracer { get; init; }

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume RPC-related code is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "We assume RPC-related code is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume RPC-related code is fully preserved")]
    public RpcMethodDef(RpcServiceDef service, MethodInfo methodInfo)
        : base(service.Type, methodInfo)
    {
        Service = service;
        Hub = service.Hub;
        NoWait = UnwrappedReturnType == typeof(RpcNoWait);
        var nameSuffix = $":{ParameterTypes.Length}";
        Name = MethodInfo.Name + nameSuffix;
        HasPolymorphicArguments = ParameterTypes.Any(RpcArgumentSerializer.IsPolymorphic);
        HasPolymorphicResult = RpcArgumentSerializer.IsPolymorphic(UnwrappedReturnType);

        if (!IsAsyncMethod) { // Invalid method
            IsValid = false;
            return;
        }

        if (service.IsSystem) { // System method
            Kind = RpcMethodKind.System;
            SystemMethodKind = service.Type == typeof(IRpcSystemCalls)
                ? MethodInfo.Name switch {
                    nameof(IRpcSystemCalls.Ok) => RpcSystemMethodKind.Ok,
                    nameof(IRpcSystemCalls.Error) => RpcSystemMethodKind.Error,
                    nameof(IRpcSystemCalls.I) => RpcSystemMethodKind.Item,
                    nameof(IRpcSystemCalls.B) => RpcSystemMethodKind.Batch,
                    _ => StreamingMethodNames.Contains(methodInfo.Name)
                        ? RpcSystemMethodKind.OtherStreaming
                        : RpcSystemMethodKind.OtherNonStreaming,
                }
                : RpcSystemMethodKind.OtherNonStreaming;
            // InboundCallFilter, InboundCallPreprocessors, InboundCallValidator
            // must be the default ones for system calls.
            InboundCallServerInvoker = GetCachedFunc<Func<ArgumentList, Task>>(typeof(InboundCallServerInvokerFactory<>));
            InboundCallPipelineInvoker = GetCachedFunc<Func<RpcInboundCall, Task>>(typeof(InboundCallPipelineInvokerFactory<>));
            return;
        }

        // Non-system method

        // ReSharper disable once VirtualMemberCallInConstructor
        Kind = GetMethodKind(out var isBackend);
        IsBackend = service.IsBackend || isBackend;
        LegacyNames = new LegacyNames(MethodInfo, nameSuffix);

        // Inbound call related
#pragma warning disable CA2214
        // ReSharper disable once VirtualMemberCallInConstructor
        InboundCallFilter = CreateInboundCallFilter();
        // ReSharper disable once VirtualMemberCallInConstructor
        InboundCallPreprocessors = CreateInboundCallPreprocessors();
        // ReSharper disable once VirtualMemberCallInConstructor
        InboundCallValidator = CreateInboundCallValidator();
#pragma warning restore CA2214
        InboundCallServerInvoker = GetCachedFunc<Func<ArgumentList, Task>>(typeof(InboundCallServerInvokerFactory<>));
        InboundCallPipelineInvoker = GetCachedFunc<Func<RpcInboundCall, Task>>(typeof(InboundCallPipelineInvokerFactory<>));

        // Outbound call related
        OutboundCallTimeouts = Hub.OutboundCallOptions.TimeoutsFactory.Invoke(this);
        OutboundCallRouter = Hub.OutboundCallOptions.RouterFactory.Invoke(this);

        // Call tracing
        Tracer = Hub.DiagnosticsOptions.CallTracerFactory.Invoke(this);
    }

    public override string ToString()
        => _toStringCached ??= ToString(useShortName: false);

    public string ToString(bool useShortName)
    {
        var arguments = ParameterTypes.Select(t => t.GetName()).ToDelimitedString();
        var returnType = UnwrappedReturnType.GetName();
        return  $"'{(useShortName ? Name : FullName)}': ({arguments}) -> {returnType}{(IsValid ? "" : " - invalid")}";
    }

    // Protected methods

    protected virtual RpcMethodKind GetMethodKind(out bool isBackend)
    {
        if (NoWait) {
            isBackend = false;
            return RpcMethodKind.Other;
        }

        var parameterTypes = ParameterTypes;
        if (parameterTypes.Length == 2 && parameterTypes[1] == typeof(CancellationToken))
            return IsCommandType(parameterTypes[0], out isBackend)
                ? RpcMethodKind.Command
                : RpcMethodKind.Query;

        isBackend = false;
        return RpcMethodKind.Query;
    }

    protected TResult GetCachedFunc<TResult>(Type factoryType)
        => GenericInstanceCache.Get<Func<RpcMethodDef, TResult>>(factoryType, UnwrappedReturnType).Invoke(this);
}

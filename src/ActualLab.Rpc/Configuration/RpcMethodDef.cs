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

    public byte CallTypeId { get; init; } = RpcCallTypes.Regular;
    public readonly bool NoWait;
    public readonly bool HasPolymorphicArguments;
    public readonly bool HasPolymorphicResult;
    public bool IsSystem => Kind is RpcMethodKind.System;
    public readonly bool IsBackend;
    public readonly RpcMethodKind Kind;
    public readonly RpcSystemMethodKind SystemMethodKind;
    public readonly LegacyNames LegacyNames = LegacyNames.Empty;
    public RpcCallTracer? Tracer { get; init; }
    public PropertyBag Properties { get; protected set; }

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
                    nameof(IRpcSystemCalls.NotFound) => RpcSystemMethodKind.NotFound,
                    nameof(IRpcSystemCalls.I) => RpcSystemMethodKind.Item,
                    nameof(IRpcSystemCalls.B) => RpcSystemMethodKind.Batch,
                    _ => StreamingMethodNames.Contains(methodInfo.Name)
                        ? RpcSystemMethodKind.OtherStreaming
                        : RpcSystemMethodKind.OtherNonStreaming,
                }
                : RpcSystemMethodKind.OtherNonStreaming;
            return;
        }

        // Non-system method
#pragma warning disable CA2214
        // ReSharper disable once VirtualMemberCallInConstructor
        Kind = GetMethodKind(out var isBackend);
#pragma warning restore CA2214
        IsBackend = service.IsBackend || isBackend;
        LegacyNames = new LegacyNames(MethodInfo, nameSuffix);

        // Call tracing
        Tracer = Hub.DiagnosticsOptions.CallTracerFactory.Invoke(this);
    }

    /// <summary>
    /// This method is called after the constructor, but only when <see cref="MethodDef.IsValid"/> is true.
    /// </summary>
    public virtual void InitializeOverridableProperties()
    {
        InboundCallFactory = RpcInboundCall.GetFactory(this);
        OutboundCallFactory = RpcOutboundCall.GetFactory(this);

        // Inbound call related

        if (!IsSystem) {
            InboundCallFilter = CreateInboundCallFilter();
            InboundCallPreprocessors = CreateInboundCallPreprocessors();
            InboundCallValidator = CreateInboundCallValidator();
        }

        // We assume CallTypeId is set by that moment, and maybe InboundCallUseFastPipelineInvoker
        var inboundCallType = RpcCallTypeRegistry.Resolve(CallTypeId).InboundCallType;
        InboundCallUseFastPipelineInvoker ??=
            inboundCallType == typeof(RpcInboundCall<>) // It's a regular call
            && SystemMethodKind != RpcSystemMethodKind.NotFound; // And not a NotFound method, which overrides InvokeServer

        InboundCallServerInvoker = GetCachedFunc<Func<ArgumentList, Task>>(typeof(InboundCallServerInvokerFactory<>));
        InboundCallPipelineInvoker = InboundCallUseFastPipelineInvoker.Value
            ? GetCachedFunc<Func<RpcInboundCall, Task>>(typeof(InboundCallPipelineFastInvokerFactory<>))
            : GetCachedFunc<Func<RpcInboundCall, Task>>(typeof(InboundCallPipelineInvokerFactory<>));

        // Outbound call related

        OutboundCallTimeouts = Hub.OutboundCallOptions.TimeoutsFactory.Invoke(this);
        OutboundCallRouter = Hub.OutboundCallOptions.RouterFactory.Invoke(this);
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

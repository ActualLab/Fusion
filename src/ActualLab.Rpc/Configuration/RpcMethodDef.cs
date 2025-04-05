using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using Cysharp.Text;

namespace ActualLab.Rpc;

public sealed class RpcMethodDef : MethodDef
{
    private static readonly HashSet<string> StreamMethodNames
        = new(StringComparer.Ordinal) { "Ack", "AckEnd", "B", "I", "End" };

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

    public readonly ArgumentListType ArgumentListType;
    public readonly ArgumentListType ResultListType;
    public readonly bool HasObjectTypedArguments;
    public readonly bool NoWait;
    public readonly bool IsSystem;
    public readonly bool IsBackend;
    public readonly bool IsStream;
    public bool IsCommand { get; init; }
    public bool AllowArgumentPolymorphism { get; init; }
    public bool AllowResultPolymorphism { get; init; }
    public RpcCallTracer? Tracer { get; init; }
    public LegacyNames LegacyNames { get; init; }
    public PropertyBag Properties { get; init; }
    public RpcCallTimeouts Timeouts { get; init; }
    public RpcSystemCallKind SystemCallKind { get; init; }

    public RpcMethodDef(
        RpcServiceDef service,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        MethodInfo method
        ) : base(serviceType, method)
    {
        if (serviceType != service.Type)
            throw new ArgumentOutOfRangeException(nameof(serviceType));

        Hub = service.Hub;
        ArgumentListType = ArgumentListType.Get(ParameterTypes);
        ResultListType = ArgumentListType.Get(UnwrappedReturnType);
        HasObjectTypedArguments = ParameterTypes.Any(type => typeof(object) == type);
        NoWait = UnwrappedReturnType == typeof(RpcNoWait);
        IsSystem = service.IsSystem;
        IsBackend = service.IsBackend;
        IsStream = IsSystem && StreamMethodNames.Contains(method.Name);

        Service = service;
        var nameSuffix = $":{ParameterTypes.Length}";
        Name = Method.Name + nameSuffix;
        AllowResultPolymorphism = AllowArgumentPolymorphism = IsSystem || IsBackend;

        if (!IsAsyncMethod)
            IsValid = false;

        Tracer = Hub.CallTracerFactory.Invoke(this);
        LegacyNames = new LegacyNames(Method
            .GetCustomAttributes<LegacyNameAttribute>(false)
            .Select(x => LegacyName.New(x, nameSuffix)));

        IsCommand = ParameterTypes.Length == 2
            && ParameterTypes[1] == typeof(CancellationToken)
            && Hub.CommandTypeDetector(ParameterTypes[0]);
        Timeouts = Hub.CallTimeoutsProvider(this).Normalize();

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
        => ZString.Concat(serviceName, '.', methodName);

    public static (string ServiceName, string MethodName) SplitFullName(string fullName)
    {
        var dotIndex = fullName.LastIndexOf('.');
        if (dotIndex < 0)
            return ("", fullName);

        var serviceName = fullName[..dotIndex];
        var methodName = fullName[(dotIndex + 1)..];
        return (serviceName, methodName);
    }
}

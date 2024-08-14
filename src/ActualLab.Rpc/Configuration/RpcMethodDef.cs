using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc.Diagnostics;

namespace ActualLab.Rpc;

public sealed class RpcMethodDef : MethodDef
{
    private static readonly HashSet<string> StreamMethodNames
        = new(StringComparer.Ordinal) { "Ack", "AckEnd", "B", "I", "End" };

    private string? _toStringCached;
    private readonly Symbol _name;

    public RpcHub Hub { get; }
    public RpcServiceDef Service { get; }
    public Symbol Name {
        get => _name;
        init {
            if (value.IsEmpty)
                throw new ArgumentOutOfRangeException(nameof(value));

            _name = value;
            FullName = $"{Service.Name.Value}.{value}";
        }
    }

    public new readonly Symbol FullName;

    public readonly Type ArgumentListType;
    public readonly bool HasObjectTypedArguments;
    public readonly Func<ArgumentList> ArgumentListFactory;
    public readonly Func<ArgumentList> ResultListFactory;
    public readonly bool NoWait;
    public readonly bool IsSystem;
    public readonly bool IsBackend;
    public readonly bool IsStream;
    public bool IsCommand { get; init; }
    public bool AllowArgumentPolymorphism { get; init; }
    public bool AllowResultPolymorphism { get; init; }
    public RpcCallTracer? Tracer { get; init; }
    public LegacyNames LegacyNames { get; init; }
    public PropertyBag CustomProperties { get; init; } = PropertyBag.Empty;
    public RpcCallTimeouts Timeouts { get; init; }

    public RpcMethodDef(
        RpcServiceDef service,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        MethodInfo method
        ) : base(serviceType, method)
    {
        if (serviceType != service.Type)
            throw new ArgumentOutOfRangeException(nameof(serviceType));

        Hub = service.Hub;
        ArgumentListType = ArgumentList.GetListType(ParameterTypes);
        HasObjectTypedArguments = ParameterTypes.Any(type => typeof(object) == type);
        NoWait = UnwrappedReturnType == typeof(RpcNoWait);
        IsSystem = service.IsSystem;
        IsBackend = service.IsBackend;
        IsStream = IsSystem && StreamMethodNames.Contains(method.Name);

        Service = service;
        var nameSuffix = $":{ParameterTypes.Length}";
        Name = Method.Name + nameSuffix;
        AllowResultPolymorphism = AllowArgumentPolymorphism = IsSystem || IsBackend;

#pragma warning disable IL2055, IL2072
        ArgumentListFactory = (Func<ArgumentList>)ArgumentListType.GetConstructorDelegate()!;
        ResultListFactory = (Func<ArgumentList>)ArgumentList.NativeTypes[1]
            .MakeGenericType(UnwrappedReturnType)
            .GetConstructorDelegate()!;
#pragma warning restore IL2055, IL2072

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
    }

    public override string ToString()
        => _toStringCached ??= ToString(useShortName: false);

    public string ToString(bool useShortName)
    {
        var arguments = ParameterTypes.Select(t => t.GetName()).ToDelimitedString();
        var returnType = UnwrappedReturnType.GetName();
        return  $"'{(useShortName ? Name : FullName)}': ({arguments}) -> {returnType}{(IsValid ? "" : " - invalid")}";
    }
}

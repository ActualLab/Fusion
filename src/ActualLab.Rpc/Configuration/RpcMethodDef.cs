using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Interception.Interceptors;
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
    public new Symbol FullName { get; private init; }

    public Type ArgumentListType { get; }
    public bool HasObjectTypedArguments { get; }
    public Func<ArgumentList> ArgumentListFactory { get; }
    public Func<ArgumentList> ResultListFactory { get; }
    public bool NoWait { get; }
    public bool IsSystem { get; }
    public bool IsBackend { get; }
    public bool IsStream { get; }
    public bool AllowArgumentPolymorphism { get; init; }
    public bool AllowResultPolymorphism { get; init; }
    public RpcMethodTracer? Tracer { get; init; }

    public RpcMethodDef(
        RpcServiceDef service,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        MethodInfo method
        ) : base(serviceType, method)
    {
        if (serviceType != service.Type)
            throw new ArgumentOutOfRangeException(nameof(serviceType));

        Hub = service.Hub;
        ArgumentListType = Parameters.Length == 0
            ? ArgumentList.Types[0]
            : ArgumentList.Types[Parameters.Length].MakeGenericType(ParameterTypes);
        HasObjectTypedArguments = ParameterTypes.Any(type => typeof(object) == type);
        NoWait = UnwrappedReturnType == typeof(RpcNoWait);
        IsSystem = service.IsSystem;
        IsBackend = service.IsBackend;
        IsStream = IsSystem && StreamMethodNames.Contains(method.Name);

        Service = service;
        Name =  $"{Method.Name}:{ParameterTypes.Length}";
        AllowResultPolymorphism = AllowArgumentPolymorphism = IsSystem || IsBackend;

#pragma warning disable IL2055, IL2072
        ArgumentListFactory = (Func<ArgumentList>)ArgumentListType.GetConstructorDelegate()!;
        ResultListFactory = (Func<ArgumentList>)ArgumentList.Types[1]
            .MakeGenericType(UnwrappedReturnType)
            .GetConstructorDelegate()!;
#pragma warning restore IL2055, IL2072

        if (!IsAsyncMethod)
            IsValid = false;

        Tracer = Hub.MethodTracerFactory.Invoke(this);
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

using ActualLab.Internal;

namespace ActualLab.DependencyInjection;

public sealed class ServiceResolver
{
    public Type Type { get; }
    public Func<IServiceProvider, object>? Resolver { get; }

    public static ServiceResolver New(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        Func<IServiceProvider, object>? resolver = null)
        => new(type, resolver);
    public static ServiceResolver New<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>(
        Func<IServiceProvider, TService>? resolver = null)
        where TService : class
        => new(typeof(TService), resolver);

    private ServiceResolver(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        Func<IServiceProvider, object>? resolver)
    {
        if (type.IsValueType)
            throw Errors.MustBeClass(type, nameof(type));

        Type = type;
        Resolver = resolver;
    }

    public override string ToString()
        => Resolver is null
            ? Type.GetName()
            : "*" + Type.GetName();

    public static implicit operator ServiceResolver(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
        => New(type);
}

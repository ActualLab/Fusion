using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception.Interceptors;

namespace ActualLab.Interception;

public interface ITypeViewFactory
{
    object CreateView(object implementation, Type viewType);
    TypeViewFactory<TView> For<TView>()
        where TView : class;
}

public class TypeViewFactory(TypeViewInterceptor interceptor) : ITypeViewFactory
{
    private static readonly TypeViewInterceptor DefaultInterceptor = new(
        TypeViewInterceptor.Options.Default,
        DependencyInjection.ServiceProviderExt.Empty);

    public static ITypeViewFactory Default { get; set; } = new TypeViewFactory(DefaultInterceptor);

    protected Interceptor Interceptor { get; } = interceptor;

#pragma warning disable IL2092
    public object CreateView(
        object implementation,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type viewType)
#pragma warning restore IL2092
    {
        if (!viewType.IsInterface)
            throw new ArgumentOutOfRangeException(nameof(viewType));

        return Proxies.New(viewType, Interceptor, implementation, false);
    }

    public TypeViewFactory<TView> For<TView>()
        where TView : class
        => new(this);
}

public readonly struct TypeViewFactory<TView>(ITypeViewFactory factory)
    where TView : class
{
    public ITypeViewFactory Factory { get; } = factory;

    public TView CreateView(object implementation)
        => (TView)Factory.CreateView(implementation, typeof(TView));
}

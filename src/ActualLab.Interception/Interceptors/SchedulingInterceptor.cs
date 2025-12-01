using ActualLab.Concurrency;

namespace ActualLab.Interception.Interceptors;

public sealed class SchedulingInterceptor : Interceptor
{
    public new record Options : Interceptor.Options
    {
        public static Options Default { get; set; } = new();
    }

    public Func<Invocation, TaskFactory?> TaskFactoryResolver { get; init; }
        = static invocation => (invocation.Proxy as IHasTaskFactory)?.TaskFactory;
    public Interceptor? NextInterceptor { get; init; }

    // ReSharper disable once ConvertToPrimaryConstructor
    public SchedulingInterceptor(Options settings, IServiceProvider services)
        : base(settings, services)
    { }

    protected internal override Func<Invocation, object?>? CreateTypedHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        Invocation initialInvocation, MethodDef methodDef)
    {
        if (!methodDef.IsAsyncMethod)
            return static invocation => invocation.InvokeInterceptedUntyped();

        var asyncInvoker = NextInterceptor is not null
            ? invocation => methodDef.InterceptorAsyncInvoker.Invoke(NextInterceptor, invocation)
            : methodDef.InterceptedAsyncInvoker;
        var taskFactoryResolver = TaskFactoryResolver;
        return invocation => {
            var taskFactory = taskFactoryResolver.Invoke(invocation);
            if (taskFactory is null)
                return invocation.InvokeInterceptedUntyped();

#pragma warning disable CA2008 // Do not create tasks without passing a TaskScheduler
            var task = taskFactory.StartNew(() => (Task<TUnwrapped>)asyncInvoker.Invoke(invocation)).Unwrap();
#pragma warning restore CA2008
            return methodDef.ReturnsValueTask
                ? methodDef.IsAsyncVoidMethod
                    ? new ValueTask(task)
                    : new ValueTask<TUnwrapped>(task)
                : task;
        };
    }
}

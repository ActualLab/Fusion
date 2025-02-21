using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Interception.Interceptors;

public sealed class ScopedServiceInterceptor : Interceptor
{
    public new record Options : Interceptor.Options
    {
        public static Options Default { get; set; } = new();
    }

    public required Type ScopedServiceType { get; init; }

    // ReSharper disable once ConvertToPrimaryConstructor
    public ScopedServiceInterceptor(Options settings, IServiceProvider services)
        : base(settings, services)
    { }

    protected internal override Func<Invocation, object?>? CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        Invocation initialInvocation, MethodDef methodDef)
    {
        var rawInvoker = initialInvocation.Arguments.GetInvoker(methodDef.Method);
        if (!methodDef.IsAsyncMethod)
            return invocation => {
                using var scope = Services.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService(ScopedServiceType);
                return rawInvoker.Invoke(service, invocation.Arguments);
            };

        var asyncInvoker = methodDef.TargetAsyncInvoker;
        return invocation => {
            var scope = Services.CreateScope();
            try {
                var service = scope.ServiceProvider.GetRequiredService(ScopedServiceType);
                var task = asyncInvoker.Invoke(service, invocation.Arguments);
                _ = task.ContinueWith(
                    static (_, scope1) => (scope1 as IDisposable)?.Dispose(),
                    scope, TaskScheduler.Default);
                scope = null;
                return methodDef.ReturnsValueTask
                    ? methodDef.IsAsyncVoidMethod
                        ? new ValueTask(task)
                        : new ValueTask<TUnwrapped>((Task<TUnwrapped>)task)
                    : task;
            }
            catch {
                scope?.Dispose();
                throw;
            }
        };
    }
}

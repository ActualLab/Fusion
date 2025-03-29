using ActualLab.Caching;

namespace ActualLab.Interception;

public partial class MethodDef
{
    // Nested types

    public sealed class TargetAsyncInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override Func<MethodDef, Func<object, ArgumentList, Task<T>>> Generate()
            => methodDef => {
                if (methodDef.ReturnsTask) {
                    if (methodDef.IsAsyncVoidMethod)
                        return (service, args) => {
                            var result = ((Task)args.GetInvoker(methodDef.Method).Invoke(service, args)!).ToUnitTask();
                            return result as Task<T> ?? throw new InvalidCastException();
                        };
                    return (service, args) => (Task<T>)args.GetInvoker(methodDef.Method).Invoke(service, args)!;
                }

                if (methodDef.ReturnsValueTask) {
                    if (methodDef.IsAsyncVoidMethod)
                        return (service, args) => {
                            var result = ((ValueTask)args.GetInvoker(methodDef.Method).Invoke(service, args)!).ToUnitTask();
                            return result as Task<T> ?? throw new InvalidCastException();
                        };
                    return (service, args) => ((ValueTask<T>)args
                        .GetInvoker(methodDef.Method)
                        .Invoke(service, args)!
                        ).AsTask();
                }

                // Non-async method
                return (service, args) => {
                    var result = Task.FromResult(args.GetInvoker(methodDef.Method).Invoke(service, args));
                    return result as Task<T> ?? throw new InvalidCastException();
                };
            };
    }

    public sealed class InterceptorAsyncInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override Func<MethodDef, Func<Interceptor, Invocation, Task<T>>> Generate()
            => methodDef => {
                if (methodDef.ReturnsTask)
                    return methodDef.IsAsyncVoidMethod
                        ? (interceptor, invocation) => {
                            var result = interceptor.Intercept<Task>(invocation).ToUnitTask();
                            return result as Task<T> ?? throw new InvalidCastException();
                        }
                        : (interceptor, invocation) => interceptor.Intercept<Task<T>>(invocation);

                if (methodDef.ReturnsValueTask)
                    return methodDef.IsAsyncVoidMethod
                        ? (interceptor, invocation) => {
                            var result = interceptor.Intercept<ValueTask>(invocation).ToUnitTask();
                            return result as Task<T> ?? throw new InvalidCastException();
                        }
                        : (interceptor, invocation) =>
                            interceptor.Intercept<ValueTask<T>>(invocation).AsTask();

                if (methodDef.ReturnType == typeof(void))
                    return (interceptor, invocation) => {
                        interceptor.Intercept(invocation);
                        return TaskExt.UnitTask as Task<T> ?? throw new InvalidCastException();
                    };

                return (interceptor, invocation) => Task.FromResult(interceptor.Intercept<T>(invocation));
            };
    }

    public sealed class InterceptedAsyncInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override Func<MethodDef, Func<Invocation, Task<T>>> Generate()
            => methodDef => {
                if (methodDef.ReturnsTask)
                    return methodDef.IsAsyncVoidMethod
                        ? invocation => {
                            var result = invocation.InvokeIntercepted<Task>().ToUnitTask();
                            return result as Task<T> ?? throw new InvalidCastException();
                        }
                        : invocation => invocation.InvokeIntercepted<Task<T>>();

                if (methodDef.ReturnsValueTask)
                    return methodDef.IsAsyncVoidMethod
                        ? invocation => {
                            var result = invocation.InvokeIntercepted<ValueTask>().ToUnitTask();
                            return result as Task<T> ?? throw new InvalidCastException();
                        }
                        : invocation => invocation.InvokeIntercepted<ValueTask<T>>().AsTask();

                if (methodDef.ReturnType == typeof(void))
                    return invocation => {
                        invocation.InvokeIntercepted();
                        return TaskExt.UnitTask as Task<T> ?? throw new InvalidCastException();
                    };

                return invocation => Task.FromResult(invocation.InvokeIntercepted<T>());
            };
    }

    public sealed class TargetObjectAsyncInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override Func<MethodDef, Func<object, ArgumentList, ValueTask<object?>>> Generate()
            => methodDef => {
                if (methodDef.ReturnsTask) {
                    if (methodDef.IsAsyncVoidMethod)
                        return async (service, args) => {
                            await ((Task)args.GetInvoker(methodDef.Method).Invoke(service, args)!)
                                .ConfigureAwait(false);
                            return null;
                        };

                    return async (service, args) => {
                        var result = await ((Task<T>)args.GetInvoker(methodDef.Method).Invoke(service, args)!)
                            .ConfigureAwait(false);
                        return result;
                    };
                }

                if (methodDef.ReturnsValueTask) {
                    if (methodDef.IsAsyncVoidMethod)
                        return async (service, args) => {
                            await ((ValueTask)args.GetInvoker(methodDef.Method).Invoke(service, args)!)
                                .ConfigureAwait(false);
                            return null;
                        };

                    return async (service, args) => {
                        var result = await ((ValueTask<T>)args
                            .GetInvoker(methodDef.Method).Invoke(service, args)!)
                            .ConfigureAwait(false);
                        return result;
                    };
                }

                // Non-async method
                return (service, args) => {
                    var result = args.GetInvoker(methodDef.Method).Invoke(service, args);
                    return new ValueTask<object?>(result);
                };
            };
    }

    public sealed class InterceptorObjectAsyncInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override Func<MethodDef, Func<Interceptor, Invocation, ValueTask<object?>>> Generate()
            => methodDef => {
                if (methodDef.ReturnsTask)
                    return methodDef.IsAsyncVoidMethod
                        ? async (interceptor, invocation) => {
                            await interceptor.Intercept<Task>(invocation).ConfigureAwait(false);
                            return null;
                        }
                        : async (interceptor, invocation) => {
                            var result = await interceptor.Intercept<Task<T>>(invocation).ConfigureAwait(false);
                            return result;
                        };

                if (methodDef.ReturnsValueTask)
                    return methodDef.IsAsyncVoidMethod
                        ? async (interceptor, invocation) => {
                            await interceptor.Intercept<ValueTask>(invocation).ConfigureAwait(false);
                            return null;
                        }
                        : async (interceptor, invocation) => {
                            var result = await interceptor.Intercept<ValueTask<T>>(invocation).ConfigureAwait(false);
                            return result;
                        };

                if (methodDef.ReturnType == typeof(void))
                    return (interceptor, invocation) => {
                        interceptor.Intercept(invocation);
                        return new ValueTask<object?>((object?)null);
                    };

                return (interceptor, invocation) => {
                    var result = interceptor.Intercept<T>(invocation);
                    return new ValueTask<object?>(result);
                };
            };
    }

    public sealed class InterceptedObjectAsyncInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override Func<MethodDef, Func<Invocation, ValueTask<object?>>> Generate()
            => methodDef => {
                if (methodDef.ReturnsTask)
                    return methodDef.IsAsyncVoidMethod
                        ? async invocation => {
                            await invocation.InvokeIntercepted<Task>().ConfigureAwait(false);
                            return null;
                        }
                        : async invocation => {
                            var result = await invocation.InvokeIntercepted<Task<T>>().ConfigureAwait(false);
                            return result;
                        };

                if (methodDef.ReturnsValueTask)
                    return methodDef.IsAsyncVoidMethod
                        ? async invocation => {
                            await invocation.InvokeIntercepted<ValueTask>().ConfigureAwait(false);
                            return null;
                        }
                        : async invocation => {
                            var result = await invocation.InvokeIntercepted<ValueTask<T>>()
                                .ConfigureAwait(false);
                            return result;
                        };

                if (methodDef.ReturnType == typeof(void))
                    return invocation => {
                        invocation.InvokeIntercepted();
                        return new ValueTask<object?>((object?)null);
                    };

                return invocation => {
                    var result = invocation.InvokeIntercepted<T>();
                    return new ValueTask<object?>(result);
                };
            };
    }

    public sealed class UniversalAsyncResultWrapperFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override Func<MethodDef, Func<Task, object?>> Generate()
            => methodDef => {
                if (!methodDef.IsAsyncMethod)
                    throw new ArgumentOutOfRangeException(nameof(methodDef), "Async method is required here.");

                if (methodDef.ReturnsTask)
                    return methodDef.IsAsyncVoidMethod
                        ? task => task // No conversion needed
                        : ToTypedTask;

                return methodDef.IsAsyncVoidMethod
                    ? task => task.ToValueTask()
                    : ToTypedValueTask;

                static Task<T> ToTypedTask(Task task) {
                    if (task is Task<T> typedTask)
                        return typedTask;

                    return ToTypedTaskAsync((Task<object?>)task);

                    static async Task<T> ToTypedTaskAsync(Task<object?> task) {
                        var result = await task.ConfigureAwait(false);
                        return (T)result!;
                    }
                }

                static object ToTypedValueTask(Task task) {
                    if (task is Task<T> typedTask)
                        return typedTask.ToValueTask();

                    return ToTypedValueTaskAsync((Task<object?>)task);

                    static async ValueTask<T> ToTypedValueTaskAsync(Task<object?> task) {
                        var result = await task.ConfigureAwait(false);
                        return (T)result!;
                    }
                }
            };
    }
}

using ActualLab.Caching;

namespace ActualLab.Interception;

public partial class MethodDef
{
    // Nested types

    public sealed class TargetAsyncInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override Func<MethodDef, Func<object, ArgumentList, Task<T>>> Generate()
            => methodDef => {
                if (methodDef.ReturnsTask)
                    return methodDef.IsAsyncVoidMethod
                        ? (service, args) => {
                            var result = ((Task)args.GetInvoker(methodDef.Method).Invoke(service, args)!).ToUnitTask();
                            return result as Task<T> ?? throw new InvalidCastException();
                        }
                        : (service, args) => (Task<T>)args.GetInvoker(methodDef.Method).Invoke(service, args)!;

                if (methodDef.ReturnsValueTask) {
                    return methodDef.IsAsyncVoidMethod
                        ? (service, args) => {
                            var result = ((ValueTask)args.GetInvoker(methodDef.Method).Invoke(service, args)!)
                                .ToUnitTask();
                            return result as Task<T> ?? throw new InvalidCastException();
                        }
                        : (service, args) => ((ValueTask<T>)args.GetInvoker(methodDef.Method).Invoke(service, args)!).AsTask();
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

                return methodDef.ReturnType == typeof(void)
                    ? (interceptor, invocation) => {
                        interceptor.Intercept(invocation);
                        return TaskExt.UnitTask as Task<T> ?? throw new InvalidCastException();
                    }
                    : (interceptor, invocation) => Task.FromResult(interceptor.Intercept<T>(invocation));
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

                return methodDef.ReturnType == typeof(void)
                    ? invocation => {
                        invocation.InvokeIntercepted();
                        return TaskExt.UnitTask as Task<T> ?? throw new InvalidCastException();
                    }
                    : invocation => Task.FromResult(invocation.InvokeIntercepted<T>());
            };
    }

    public sealed class TargetObjectAsyncInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override Func<MethodDef, Func<object, ArgumentList, ValueTask<object?>>> Generate()
            => methodDef => {
                if (methodDef.ReturnsTask) {
                    return methodDef.IsAsyncVoidMethod
                        ? (service, args) => {
                            var task = (Task)args.GetInvoker(methodDef.Method).Invoke(service, args)!;
                            var resultTask = task.ContinueWith(
                                static t => {
                                    t.GetAwaiter().GetResult();
                                    return (object?)null;
                                },
                                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                            return new ValueTask<object?>(resultTask);
                        }
                        : (service, args) => {
                            var task = (Task<T>)args.GetInvoker(methodDef.Method).Invoke(service, args)!;
                            var resultTask = task.ContinueWith(
                                static t => (object?)t.GetAwaiter().GetResult(),
                                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                            return new ValueTask<object?>(resultTask);
                        };
                }

                if (methodDef.ReturnsValueTask) {
                    return methodDef.IsAsyncVoidMethod
                        ? (service, args) => {
                            var valueTask = (ValueTask)args.GetInvoker(methodDef.Method).Invoke(service, args)!;
                            if (valueTask.IsCompletedSuccessfully)
                                return default;

                            var resultTask = valueTask.AsTask().ContinueWith(
                                static t => {
                                    t.GetAwaiter().GetResult();
                                    return (object?)null;
                                },
                                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                            return new ValueTask<object?>(resultTask);
                        }
                        : (service, args) => {
                            var valueTask = (ValueTask<T>)args.GetInvoker(methodDef.Method).Invoke(service, args)!;
                            if (valueTask.IsCompletedSuccessfully)
                                return new ValueTask<object?>(valueTask.Result);

                            var resultTask = valueTask.AsTask().ContinueWith(
                                static t => (object?)t.GetAwaiter().GetResult(),
                                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                            return new ValueTask<object?>(resultTask);
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
                        ?  (interceptor, invocation) => {
                            var task = interceptor.Intercept<Task>(invocation)!;
                            var resultTask = task.ContinueWith(
                                static t => {
                                    t.GetAwaiter().GetResult();
                                    return (object?)null;
                                },
                                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                            return new ValueTask<object?>(resultTask);
                        }
                        :  (interceptor, invocation) => {
                            var task = interceptor.Intercept<Task<T>>(invocation)!;
                            var resultTask = task.ContinueWith(
                                static t => (object?)t.GetAwaiter().GetResult(),
                                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                            return new ValueTask<object?>(resultTask);
                        };

                if (methodDef.ReturnsValueTask)
                    return methodDef.IsAsyncVoidMethod
                        ? (interceptor, invocation) => {
                            var valueTask = interceptor.Intercept<ValueTask>(invocation);
                            if (valueTask.IsCompletedSuccessfully)
                                return default;

                            var resultTask = valueTask.AsTask().ContinueWith(
                                static t => {
                                    t.GetAwaiter().GetResult();
                                    return (object?)null;
                                },
                                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                            return new ValueTask<object?>(resultTask);
                        }
                        : (interceptor, invocation) => {
                            var valueTask = interceptor.Intercept<ValueTask<T>>(invocation);
                            if (valueTask.IsCompletedSuccessfully)
                                return new ValueTask<object?>(valueTask.Result);

                            var resultTask = valueTask.AsTask().ContinueWith(
                                static t => (object?)t.GetAwaiter().GetResult(),
                                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                            return new ValueTask<object?>(resultTask);
                        };

                return methodDef.ReturnType == typeof(void)
                    ? (interceptor, invocation) => {
                        interceptor.Intercept(invocation);
                        return default;
                    }
                    : (interceptor, invocation) => {
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
                        ? invocation => {
                            var task = invocation.InvokeIntercepted<Task>();
                            var resultTask = task.ContinueWith(
                                static t => {
                                    t.GetAwaiter().GetResult();
                                    return (object?)null;
                                },
                                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                            return new ValueTask<object?>(resultTask);
                        }
                        : invocation => {
                            var task = invocation.InvokeIntercepted<Task<T>>();
                            var resultTask = task.ContinueWith(
                                static t => (object?)t.GetAwaiter().GetResult(),
                                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                            return new ValueTask<object?>(resultTask);
                        };

                if (methodDef.ReturnsValueTask)
                    return methodDef.IsAsyncVoidMethod
                        ? invocation => {
                            var valueTask = invocation.InvokeIntercepted<ValueTask>();
                            if (valueTask.IsCompletedSuccessfully)
                                return default;

                            var resultTask = valueTask.AsTask().ContinueWith(
                                static t => {
                                    t.GetAwaiter().GetResult();
                                    return (object?)null;
                                },
                                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                            return new ValueTask<object?>(resultTask);
                        }
                        : invocation => {
                            var valueTask = invocation.InvokeIntercepted<ValueTask<T>>();
                            if (valueTask.IsCompletedSuccessfully)
                                return new ValueTask<object?>(valueTask.Result);

                            var resultTask = valueTask.AsTask().ContinueWith(
                                static t => (object?)t.GetAwaiter().GetResult(),
                                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                            return new ValueTask<object?>(resultTask);
                        };

                return methodDef.ReturnType == typeof(void)
                    ? invocation => {
                        invocation.InvokeIntercepted();
                        return default;
                    }
                    : invocation => {
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
                        ? static task => task // No conversion needed
                        : ToTypedTask;

                return methodDef.IsAsyncVoidMethod
                    ? static task => task.ToValueTask()
                    : ToTypedValueTask;

                static Task<T> ToTypedTask(Task task) {
                    if (task is Task<T> typedTask)
                        return typedTask;

                    return ToTypedTaskAsync((Task<object?>)task);

                    static Task<T> ToTypedTaskAsync(Task<object?> task)
                        => task.ContinueWith(
                            static t => {
                                var result = t.GetAwaiter().GetResult();
                                return (T)result!;
                            },
                            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }

                static object ToTypedValueTask(Task task) {
                    if (task is Task<T> typedTask)
                        return typedTask.ToValueTask();

                    return ToTypedValueTaskAsync((Task<object?>)task);

                    static ValueTask<T> ToTypedValueTaskAsync(Task<object?> task) {
                        if (task.IsCompletedSuccessfully)
                            return new ValueTask<T>((T)task.Result!);

                        var resultTask = task.ContinueWith(
                            static t => {
                                var result = t.GetAwaiter().GetResult();
                                return (T)result!;
                            },
                            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                        return new ValueTask<T>(resultTask);
                    }
                }
            };
    }
}

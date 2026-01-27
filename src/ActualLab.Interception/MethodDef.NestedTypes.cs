using ActualLab.Caching;

namespace ActualLab.Interception;

public partial class MethodDef
{
    // Nested types

    public sealed class TargetAsyncInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override object Generate()
            => (MethodDef methodDef) => {
                var invoker = methodDef.ArgumentListInvoker;
                if (methodDef.ReturnsTask)
                    return (Func<object, ArgumentList, Task<T>>)(methodDef.IsAsyncVoidMethod
                        ? (service, args) => {
                            var result = ((Task)invoker.Invoke(service, args)!).ToUnitTask();
                            return result as Task<T> ?? throw new InvalidCastException();
                        }
                        : (service, args)
                            => (Task<T>)invoker.Invoke(service, args)!);

                if (methodDef.ReturnsValueTask) {
                    return methodDef.IsAsyncVoidMethod
                        ? (service, args) => {
                            var result = ((ValueTask)invoker.Invoke(service, args)!).ToUnitTask();
                            return result as Task<T> ?? throw new InvalidCastException();
                        }
                        : (service, args)
                            => ((ValueTask<T>)invoker.Invoke(service, args)!).AsTask();
                }

                // Non-async method
                return (service, args) => {
                    var result = Task.FromResult(invoker.Invoke(service, args));
                    return result as Task<T> ?? throw new InvalidCastException();
                };
            };
    }

    public sealed class InterceptorAsyncInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override object Generate()
            => (MethodDef methodDef) => {
                if (methodDef.ReturnsTask)
                    return (Func<Interceptor, Invocation, Task<T>>)(methodDef.IsAsyncVoidMethod
                        ? (interceptor, invocation) => {
                            var result = interceptor.Intercept<Task>(invocation).ToUnitTask();
                            return result as Task<T> ?? throw new InvalidCastException();
                        }
                        : (interceptor, invocation)
                            => interceptor.Intercept<Task<T>>(invocation));

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
                    : (interceptor, invocation)
                        => Task.FromResult(interceptor.Intercept<T>(invocation));
            };
    }

    public sealed class InterceptedAsyncInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override object Generate()
            => (MethodDef methodDef) => {
                if (methodDef.ReturnsTask)
                    return (Func<Invocation, Task<T>>)(methodDef.IsAsyncVoidMethod
                        ? invocation => {
                            var result = invocation.InvokeIntercepted<Task>().ToUnitTask();
                            return result as Task<T> ?? throw new InvalidCastException();
                        }
                        : invocation => invocation.InvokeIntercepted<Task<T>>());

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
        public override object Generate()
            => (MethodDef methodDef) => {
                var invoker = methodDef.ArgumentListInvoker;
                if (methodDef.ReturnsTask) {
                    return (Func<object, ArgumentList, ValueTask<object?>>)(methodDef.IsAsyncVoidMethod
                        ? (service, args) => {
                            var task = (Task)invoker.Invoke(service, args)!;
                            var resultTask = task.ContinueWith(
                                static t => {
                                    t.GetAwaiter().GetResult();
                                    return (object?)null;
                                },
                                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                            return new ValueTask<object?>(resultTask);
                        }
                        : (service, args) => {
                            var task = (Task<T>)invoker.Invoke(service, args)!;
                            var resultTask = task.ContinueWith(
                                static t => (object?)t.GetAwaiter().GetResult(),
                                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                            return new ValueTask<object?>(resultTask);
                        });
                }

                if (methodDef.ReturnsValueTask) {
                    return methodDef.IsAsyncVoidMethod
                        ? (service, args) => {
                            var valueTask = (ValueTask)invoker.Invoke(service, args)!;
                            if (valueTask.IsCompletedSuccessfully)
                                return default;

                            var task = valueTask.AsTask();
                            var resultTask = task.ContinueWith(
                                static t => {
                                    t.GetAwaiter().GetResult();
                                    return (object?)null;
                                },
                                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                            return new ValueTask<object?>(resultTask);
                        }
                        : (service, args) => {
                            var valueTask = (ValueTask<T>)invoker.Invoke(service, args)!;
                            if (valueTask.IsCompletedSuccessfully)
                                return new ValueTask<object?>(valueTask.Result);

                            var task = valueTask.AsTask();
                            var resultTask = task.ContinueWith(
                                static t => (object?)t.GetAwaiter().GetResult(),
                                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                            return new ValueTask<object?>(resultTask);
                        };
                }

                // Non-async method
                return (service, args) => {
                    var result = invoker.Invoke(service, args);
                    return new ValueTask<object?>(result);
                };
            };
    }

    public sealed class InterceptorObjectAsyncInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override object Generate()
            => (MethodDef methodDef) => {
                if (methodDef.ReturnsTask)
                    return (Func<Interceptor, Invocation, ValueTask<object?>>)(methodDef.IsAsyncVoidMethod
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
                        });

                if (methodDef.ReturnsValueTask)
                    return methodDef.IsAsyncVoidMethod
                        ? (interceptor, invocation) => {
                            var valueTask = interceptor.Intercept<ValueTask>(invocation);
                            if (valueTask.IsCompletedSuccessfully)
                                return default;

                            var task = valueTask.AsTask();
                            var resultTask = task.ContinueWith(
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

                            var task = valueTask.AsTask();
                            var resultTask = task.ContinueWith(
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
        public override object Generate()
            => (MethodDef methodDef) => {
                if (methodDef.ReturnsTask)
                    return (Func<Invocation, ValueTask<object?>>)(methodDef.IsAsyncVoidMethod
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
                        });

                if (methodDef.ReturnsValueTask)
                    return methodDef.IsAsyncVoidMethod
                        ? invocation => {
                            var valueTask = invocation.InvokeIntercepted<ValueTask>();
                            if (valueTask.IsCompletedSuccessfully)
                                return default;

                            var task = valueTask.AsTask();
                            var resultTask = task.ContinueWith(
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

                            var task = valueTask.AsTask();
                            var resultTask = task.ContinueWith(
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

    public sealed class UniversalAsyncResultConverterFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override object Generate()
            => (MethodDef methodDef) => {
                if (!methodDef.IsAsyncMethod)
                    throw new ArgumentOutOfRangeException(nameof(methodDef), "Async method is required here.");

                if (methodDef.ReturnsTask)
                    return (Func<Task, object?>)(methodDef.IsAsyncVoidMethod
                        ? static task => task // No conversion needed
                        : ToTypedTask);

                return methodDef.IsAsyncVoidMethod
                    ? static task => task.ToValueTask()
                    : ToTypedValueTask;

                static object ToTypedTask(Task task) {
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
                            return new ValueTask<T>((T)task.GetAwaiter().GetResult()!);

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

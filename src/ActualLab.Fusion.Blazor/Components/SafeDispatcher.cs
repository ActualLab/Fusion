using ActualLab.OS;
using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Blazor;

/// <summary>
/// A <see cref="Microsoft.AspNetCore.Components.Dispatcher"/> wrapper that never queues a work item without an
/// <see cref="ExecutionContext"/>.
/// See https://github.com/dotnet/aspnetcore/issues/69323.
/// </summary>
public sealed class SafeDispatcher : Dispatcher
{
    // Blazor's own dispatcher may run a queued work item inline on whichever thread frees the renderer
    // next. Queued with ExecutionContext == null - which is what ExecutionContext.SuppressFlow() produces -
    // such a work item leaves the renderer's SynchronizationContext on that thread, so CheckAccess()
    // starts returning true there and the thread renders past the dispatcher's queue.
    // Only Blazor's own dispatcher is affected: MAUI, WPF and WinForms supply their own, and
    // WebAssemblyDispatcher compares thread ids rather than synchronization contexts.
    private const string UnsafeDispatcherFullTypeName =
        "Microsoft.AspNetCore.Components.Rendering.RendererSynchronizationContextDispatcher";

    private static Type? _unsafeDispatcherType;

    public static bool IsEnabled { get; set; } = !OSInfo.IsAnyClient;

    // The generic overloads can't cache their invokers per instance, so they pass the instance as
    // the state and take the work item from here instead.
    [ThreadStatic] private static object? _workItem;

    private readonly Func<object?, Task> _invokeAction;
    private readonly Func<object?, Task> _invokeAsyncAction;

    public Dispatcher Dispatcher { get; }

    public static Dispatcher WrapIfUnsafe(Dispatcher dispatcher)
        => IsUnsafe(dispatcher)
            ? new SafeDispatcher(dispatcher)
            : dispatcher;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUnsafe(Dispatcher dispatcher)
        => IsEnabled && IsUnsafe(dispatcher.GetType());

    public static bool IsUnsafe(Type dispatcherType)
    {
        if (dispatcherType is null)
            throw new ArgumentNullException(nameof(dispatcherType));

        if (dispatcherType == _unsafeDispatcherType)
            return true;

        if (!string.Equals(dispatcherType.FullName, UnsafeDispatcherFullTypeName, StringComparison.Ordinal))
            return false;

        _unsafeDispatcherType = dispatcherType;
        return true;
    }

    private SafeDispatcher(Dispatcher dispatcher)
    {
        Dispatcher = dispatcher;
        _invokeAction = state => Dispatcher.InvokeAsync((Action)state!);
        _invokeAsyncAction = state => Dispatcher.InvokeAsync((Func<Task>)state!);
    }

    public override bool CheckAccess()
        => Dispatcher.CheckAccess();

    public override Task InvokeAsync(Action workItem)
        => ExecutionContext.IsFlowSuppressed()
            ? ExecutionContextExt.StartWithDefaultExecutionContext(_invokeAction, workItem)
            : Dispatcher.InvokeAsync(workItem);

    public override Task InvokeAsync(Func<Task> workItem)
        => ExecutionContext.IsFlowSuppressed()
            ? ExecutionContextExt.StartWithDefaultExecutionContext(_invokeAsyncAction, workItem)
            : Dispatcher.InvokeAsync(workItem);

    public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem)
        => ExecutionContext.IsFlowSuppressed()
            ? InvokeWithDefaultExecutionContext(Cache<TResult>.Func, workItem)
            : Dispatcher.InvokeAsync(workItem);

    public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem)
        => ExecutionContext.IsFlowSuppressed()
            ? InvokeWithDefaultExecutionContext(Cache<TResult>.FuncTask, workItem)
            : Dispatcher.InvokeAsync(workItem);

    // Private methods

    private Task<TResult> InvokeWithDefaultExecutionContext<TResult>(
        Func<object?, Task<TResult>> invoker,
        object workItem)
    {
        var oldWorkItem = _workItem;
        try {
            _workItem = workItem;
            return ExecutionContextExt.StartWithDefaultExecutionContext(invoker, this);
        }
        finally {
            _workItem = oldWorkItem;
        }
    }

    // Nested types

    private static class Cache<TResult>
    {
        public static readonly Func<object?, Task<TResult>> Func =
            static state => ((SafeDispatcher)state!).Dispatcher.InvokeAsync((Func<TResult>)_workItem!);
        public static readonly Func<object?, Task<TResult>> FuncTask =
            static state => ((SafeDispatcher)state!).Dispatcher.InvokeAsync((Func<Task<TResult>>)_workItem!);
    }
}

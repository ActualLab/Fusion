using System.Linq.Expressions;
using System.Reflection.Emit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;

namespace ActualLab.Fusion.Blazor;

#pragma warning disable BL0006

public static class ComponentExt
{
#if USE_UNSAFE_ACCESSORS
    // [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_renderFragment")]
    // private static extern ref RenderFragment RenderFragmentGetter(ComponentBase @this);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_renderHandle")]
    private static extern ref RenderHandle RenderHandleGetter(ComponentBase @this);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_initialized")]
    private static extern ref bool IsInitializedGetter(ComponentBase @this);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_initialized")]
    private static extern ref bool HasPendingQueuedRenderGetter(ComponentBase @this);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_hasPendingQueuedRender")]
    private static extern ref Renderer? RendererGetter(ref RenderHandle @this);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_componentId")]
    private static extern ref int ComponentIdGetter(ref RenderHandle @this);
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "StateHasChanged")]
    private static extern void StateHasChangedInvoker(ComponentBase @this);
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetOptionalComponentState")]
    private static extern ComponentState? GetOptionalComponentStateGetter(Renderer @this, int componentId);

    private static ComponentState? GetOptionalComponentStateGetter(RenderHandle renderHandle)
    {
        var renderer = RendererGetter(ref renderHandle);
        if (renderer is null)
            return null;

        var componentId = ComponentIdGetter(ref renderHandle);
        return GetOptionalComponentStateGetter(renderer, componentId);
    }
#else
    // private static readonly Func<ComponentBase, RenderFragment> RenderFragmentGetter;
    // private static readonly Action<ComponentBase, RenderFragment> RenderFragmentSetter;
    private static readonly Func<ComponentBase, RenderHandle> RenderHandleGetter;
    private static readonly Func<ComponentBase, bool> IsInitializedGetter;
    private static readonly Action<ComponentBase, bool> IsInitializedSetter;
    private static readonly Func<ComponentBase, bool> HasPendingQueuedRenderGetter;
    private static readonly Action<ComponentBase, bool> HasPendingQueuedRenderSetter;
    private static readonly Action<ComponentBase> StateHasChangedInvoker;
    private static readonly Func<RenderHandle, object?> GetOptionalComponentStateGetter;
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RenderHandle GetRenderHandle(this ComponentBase component)
        => RenderHandleGetter(component);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dispatcher GetDispatcher(this ComponentBase component)
        => RenderHandleGetter(component).Dispatcher;

/*
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RenderFragment GetRenderFragment(ComponentBase component)
        => RenderFragmentGetter(component);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetRenderFragment(ComponentBase component, RenderFragment renderFragment)
#if USE_UNSAFE_ACCESSORS
        => RenderFragmentGetter(component) = renderFragment;
#else
        => RenderFragmentSetter(component, renderFragment);
#endif
*/

    /// <summary>
    /// Calls <see cref="ComponentBase.StateHasChanged"/> in the Blazor synchronization context
    /// of the component, therefore, it works even when called from another synchronization context
    /// (e.g., a thread-pool thread).
    /// </summary>
    public static void NotifyStateHasChanged(this ComponentBase component)
    {
        try {
            var dispatcher = component.GetDispatcher();
            if (dispatcher.CheckAccess()) // Also handles NullDispatcher, which always returns true here
                StateHasChangedInvoker(component);
            else if (component is CircuitHubComponentBase fc)
                _ = dispatcher.InvokeAsync(fc.StateHasChangedInvoker);
            else
                _ = dispatcher.InvokeAsync(() => StateHasChangedInvoker(component));
        }
        catch (ObjectDisposedException) {
            // Intended
        }
    }

    // Other handy methods - exposed as regular rather than extension methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInitialized(ComponentBase component)
        => IsInitializedGetter(component);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MarkInitialized(ComponentBase component)
#if USE_UNSAFE_ACCESSORS
        => IsInitializedGetter(component) = true;
#else
        => IsInitializedSetter(component, true);
#endif

    public static bool IsDisposed(ComponentBase component)
    {
        var renderHandle = RenderHandleGetter(component);
        return GetOptionalComponentStateGetter(renderHandle) is null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ClosedDisposable<ComponentBase> SuspendStateHasChanged(ComponentBase component)
        => HasPendingQueuedRenderGetter(component)
            ? default // Already suspended
            : Disposable.NewClosed(component, static c => {
#if USE_UNSAFE_ACCESSORS
                HasPendingQueuedRenderGetter(c) = false;
#else
                HasPendingQueuedRenderSetter(c, false);
#endif
            });

#if !USE_UNSAFE_ACCESSORS
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "See DynamicDependency below")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ComponentBase))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RenderHandle))]
    static ComponentExt()
    {
        var bfInstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
        var tComponentBase = typeof(ComponentBase);
        var fInitialized = tComponentBase.GetField("_initialized", bfInstanceNonPublic)!;
        var fHasPendingQueuedRender = tComponentBase.GetField("_hasPendingQueuedRender", bfInstanceNonPublic)!;
        var fRenderHandle = tComponentBase.GetField("_renderHandle", bfInstanceNonPublic)!;
        var mStateHasChanged = tComponentBase.GetMethod("StateHasChanged", bfInstanceNonPublic)!;

        IsInitializedGetter = fInitialized.GetGetter<ComponentBase, bool>();
        IsInitializedSetter = fInitialized.GetSetter<ComponentBase, bool>();
        HasPendingQueuedRenderGetter = fHasPendingQueuedRender.GetGetter<ComponentBase, bool>();
        HasPendingQueuedRenderSetter = fHasPendingQueuedRender.GetSetter<ComponentBase, bool>();
        // RenderFragmentGetter = fRenderFragment.GetGetter<ComponentBase, RenderFragment>();
        // RenderFragmentSetter = fRenderFragment.GetSetter<ComponentBase, RenderFragment>();
        RenderHandleGetter = fRenderHandle.GetGetter<ComponentBase, RenderHandle>();
        StateHasChangedInvoker = (Action<ComponentBase>)mStateHasChanged.CreateDelegate(typeof(Action<ComponentBase>));

        GetOptionalComponentStateGetter = RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? CreateOptionalComponentStateGetterDM()
            : CreateOptionalComponentStateGetterET();
    }

    private static Func<RenderHandle, object?> CreateOptionalComponentStateGetterDM()
    {
        var bfInstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
        var tRenderHandle = typeof(RenderHandle);
        var tRenderer = typeof(Renderer);
        var fComponentId = tRenderHandle.GetField("_componentId", bfInstanceNonPublic)!;
        var fRenderer = tRenderHandle.GetField("_renderer", bfInstanceNonPublic)!;
        var mGetOptionalComponentState = tRenderer.GetMethod("GetOptionalComponentState", bfInstanceNonPublic)!;

        var m = new DynamicMethod("_GetOptionalComponentState", typeof(object), [typeof(RenderHandle)], true);
        var il = m.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, fRenderer);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, fComponentId);
        il.Emit(OpCodes.Callvirt, mGetOptionalComponentState);
        il.Emit(OpCodes.Ret);
        return (Func<RenderHandle, object?>)m.CreateDelegate(typeof(Func<RenderHandle, object?>));
    }

    private static Func<RenderHandle, object?> CreateOptionalComponentStateGetterET()
    {
        var bfInstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
        var tRenderHandle = typeof(RenderHandle);
        var tRenderer = typeof(Renderer);
        var fComponentId = tRenderHandle.GetField("_componentId", bfInstanceNonPublic)!;
        var fRenderer = tRenderHandle.GetField("_renderer", bfInstanceNonPublic)!;
        var mGetOptionalComponentState = tRenderer.GetMethod("GetOptionalComponentState", bfInstanceNonPublic)!;

        var pRenderHandle = Expression.Parameter(tRenderHandle, "renderHandle");
        var eBody = Expression.Call(
            Expression.Field(pRenderHandle, fRenderer),
            mGetOptionalComponentState,
            Expression.Field(pRenderHandle, fComponentId));
        return (Func<RenderHandle, object?>)Expression
            .Lambda(typeof(Func<RenderHandle, object?>), eBody, pRenderHandle)
            .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
    }
#endif
}

using System.Reflection;
using System.Runtime.CompilerServices;
using ActualLab.Fusion.Blazor;
using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Tests.Blazor;

public class FusionBlazorCoreAuditRegressionTest
{
    [Fact]
    public void ComputedAndMutableStateCategoriesShouldUseIndependentCaches()
    {
        var componentType = typeof(CategoryProbeComponent);

        var mutableCategory = ComputedStateComponent.GetMutableStateCategory(componentType);
        var computedCategory = ComputedStateComponent.GetStateCategory(componentType);

        mutableCategory.Should().EndWith(".MutableState");
        computedCategory.Should().EndWith(".State");
        computedCategory.Should().NotBe(mutableCategory);
    }

    [Fact]
    public void DefaultParameterComparerShouldBeASealedBuiltInComparer()
    {
        typeof(DefaultParameterComparer).IsSealed.Should().BeTrue();
        DefaultParameterComparer.Instance.AreEqual(1, 1).Should().BeTrue();
        DefaultParameterComparer.Instance.AreEqual(new object(), new object()).Should().BeFalse();
    }

    [Fact]
    public void ComputedStateOptionsShouldExposeOnlyImplementedRenderPoints()
    {
        Enum.GetNames(typeof(ComputedStateComponentOptions))
            .Should().NotContain("UseInitializedAsyncRenderPoint");
        ComputedStateComponentOptions.UseAllRenderPoints.Should().Be(
            ComputedStateComponentOptions.UseParametersSetRenderPoint
            | ComputedStateComponentOptions.UseParametersSetAsyncRenderPoint);
    }

    [Fact]
    public void DisposedMixedStateComponentShouldReleaseItsMutableStateSubscription()
    {
        var mutableState = StateFactory.Default.NewMutable(0);

        var componentReference = CreateDisposedMixedStateComponent(mutableState);

        GetUpdatedSubscriberCount(mutableState).Should().Be(0);
        for (var i = 0; i < 3 && componentReference.IsAlive; i++) {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        componentReference.IsAlive.Should().BeFalse();
    }

#if NET8_0_OR_GREATER
    [Theory]
    [InlineData("HasPendingQueuedRenderGetter", "_hasPendingQueuedRender")]
    [InlineData("RendererGetter", "_renderer")]
    public void UnsafeAccessorsShouldTargetTheirMatchingBlazorFields(string methodName, string fieldName)
    {
        var method = typeof(ComponentExt).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!;
        var attribute = method.GetCustomAttribute<System.Runtime.CompilerServices.UnsafeAccessorAttribute>();

        attribute.Should().NotBeNull();
        attribute!.Name.Should().Be(fieldName);
    }

    [Fact]
    public void UnsafeAccessorsShouldOperateOnMatchingBlazorFields()
    {
        var component = new CategoryProbeComponent();
        var fieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        var initializedField = typeof(ComponentBase).GetField("_initialized", fieldFlags)!;
        var pendingRenderField = typeof(ComponentBase).GetField("_hasPendingQueuedRender", fieldFlags)!;
        initializedField.SetValue(component, false);
        pendingRenderField.SetValue(component, false);

        using (ComponentExt.SuspendStateHasChanged(component)) {
            initializedField.SetValue(component, true);
            pendingRenderField.SetValue(component, true);
        }

        initializedField.GetValue(component).Should().Be(true);
        pendingRenderField.GetValue(component).Should().Be(false);
        ComponentExt.IsDisposed(component).Should().BeTrue();
    }
#endif

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDisposedMixedStateComponent(MutableState<int> mutableState)
    {
        var component = new MixedStateProbeComponent();
        component.Initialize(StateFactory.Default.NewMutable(0), mutableState);
        GetUpdatedSubscriberCount(mutableState).Should().Be(1);

        var componentReference = new WeakReference(component);
        component.DisposeAsync().GetAwaiter().GetResult();
        return componentReference;
    }

    private static int GetUpdatedSubscriberCount(State state)
        => ((Delegate?)typeof(State).GetField("Updated", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(state))?.GetInvocationList().Length ?? 0;

    private sealed class CategoryProbeComponent : ComponentBase;

    private sealed class MixedStateProbeComponent : MixedStateComponent<int, int>
    {
        public void Initialize(MutableState<int> state, MutableState<int> mutableState)
        {
            SetState(state);
            SetMutableState(mutableState);
        }

        protected override Task<int> ComputeState(CancellationToken cancellationToken)
            => Task.FromResult(0);
    }
}

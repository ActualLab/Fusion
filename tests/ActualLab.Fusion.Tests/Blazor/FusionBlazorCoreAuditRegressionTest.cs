using System.Reflection;
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
#endif

    private sealed class CategoryProbeComponent : ComponentBase;
}

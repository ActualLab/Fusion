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
    public void DerivedDefaultParameterComparerShouldBeApplied()
    {
        var component = new ComparerProbeComponent();
        var componentInfo = ComponentInfo.Get(component.GetType());
        var parameterView = ParameterView.FromDictionary(new Dictionary<string, object?> {
            [nameof(ComparerProbeComponent.Value)] = "new",
        });

        componentInfo.ShouldSetParameters(component, parameterView).Should().BeFalse();
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

    private sealed class ComparerProbeComponent : ComponentBase
    {
        [Parameter]
        [ParameterComparer(typeof(AlwaysEqualParameterComparer))]
        public string Value { get; set; } = "old";
    }

    private sealed class AlwaysEqualParameterComparer : DefaultParameterComparer
    {
        public override bool AreEqual(object? oldValue, object? newValue) => true;
    }
}

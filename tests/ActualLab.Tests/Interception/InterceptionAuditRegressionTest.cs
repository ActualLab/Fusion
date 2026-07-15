using ActualLab.Interception;

namespace ActualLab.Tests.Interception;

public class InterceptionAuditRegressionTest
{
    [Fact]
    public void ProxyGeneratorMustSupportNestedTypes()
        => Proxies.TryGetProxyType(typeof(INestedProxy)).Should().NotBeNull();

    [Fact(Skip = "Known defect: the dynamic invoker crashes the process for value-type targets.")]
    public void ArgumentListInvokerMustSupportValueTypeTargets()
    {
        var method = typeof(ValueTypeTarget).GetMethod(nameof(ValueTypeTarget.GetValue))!;
        var invoker = ArgumentList.Empty.GetInvoker(method);

        invoker.Invoke(new ValueTypeTarget(42), ArgumentList.Empty).Should().Be(42);
    }

    private readonly record struct ValueTypeTarget(int Value)
    {
        public int GetValue() => Value;
    }

    public interface INestedProxy : IRequiresAsyncProxy
    {
        Task Run();
    }
}

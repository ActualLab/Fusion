using ActualLab.Resilience;
using ActualLab.Rpc;

namespace ActualLab.Tests.Rpc;

public class RpcTimeoutExceptionTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        var e = new RpcTimeoutException(RpcTimeoutKind.Connect, "Test");
        e.Should().BeAssignableTo<TimeoutException>();
        e.TimeoutKind.Should().Be(RpcTimeoutKind.Connect);
        e.Message.Should().Be("Test");
        TransiencyResolvers.PreferTransient.Invoke(e).Should().Be(Transiency.Transient);

        new RpcTimeoutException().TimeoutKind.Should().Be(RpcTimeoutKind.Unknown);
        new RpcTimeoutException("Test").TimeoutKind.Should().Be(RpcTimeoutKind.Unknown);
        new RpcTimeoutException(RpcTimeoutKind.Run).Message.Should().NotBeNullOrEmpty();
    }
}

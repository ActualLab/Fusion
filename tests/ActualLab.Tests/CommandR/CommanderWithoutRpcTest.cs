using ActualLab.Rpc;
using ActualLab.Tests.CommandR.Services;

namespace ActualLab.Tests.CommandR;

// AddCommander() registers RpcCommandHandler, which must work in a container without RPC
public class CommanderWithoutRpcTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task ServiceCommandTest()
    {
        var services = new ServiceCollection()
            .AddCommander(c => c.AddService<IMathService, MathService>())
            .BuildServiceProvider();
        services.GetService<RpcHub>().Should().BeNull();

        var result = await services.Commander().Call(new DivCommand() { Divisible = 4, Divisor = 2 });
        result.Should().Be(2);
    }
}

using ActualLab.Rpc;

namespace ActualLab.Tests.CommandR;

public class BackendCommandGateTest
{
    [Fact]
    public void CommandInterfaceNamesMustMatchTheirTypes()
    {
        // ActualLab.Rpc can't reference ActualLab.CommandR, so these names are strings there;
        // a namespace move would silently turn the backend-command gate into a no-op without this test.
        RpcMethodDef.CommandInterfaceFullName.Should().Be(typeof(ICommand).FullName);
        RpcMethodDef.BackendCommandInterfaceFullName.Should().Be(typeof(IBackendCommand).FullName);
    }

    [Fact]
    public void IsCommandTypeMustDetectBackendCommands()
    {
        RpcMethodDef.IsCommandType(typeof(TestCommand), out var isBackendCommand).Should().BeTrue();
        isBackendCommand.Should().BeFalse();

        RpcMethodDef.IsCommandType(typeof(TestBackendCommand), out isBackendCommand).Should().BeTrue();
        isBackendCommand.Should().BeTrue();

        RpcMethodDef.IsCommandType(typeof(string), out isBackendCommand).Should().BeFalse();
        isBackendCommand.Should().BeFalse();
    }

    // Nested types

    public sealed record TestCommand : ICommand<Unit>;
    public sealed record TestBackendCommand : ICommand<Unit>, IBackendCommand;
}

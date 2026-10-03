using ActualLab.CommandR.Operations;
using ActualLab.Interception;
using ActualLab.Reflection;
using MessagePack;

namespace ActualLab.Tests.CommandR;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record OperationCompletionTest_Command(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] string Key
) : ICommand<Unit>;

// OperationCompletion is persisted as an _Events payload and can be routed as a command, so it has
// to survive whichever serializer carries it - with its polymorphic inner command intact.
public class OperationCompletionTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void TheOperationConstructorCopiesItsCommandAndCalls()
    {
        var command = new OperationCompletionTest_Command("a");
        var call = NewCall();
        var operation = new Operation("uuid", "host", command: command).AddInvalidationCall(call);

        var completion = OperationCompletion.New(operation);

        completion.Command.Should().BeSameAs(command);
        completion.InvalidationCalls.Should().Equal(call);
    }

    [Fact]
    public void ACompletionCanCarryNoCommand()
    {
        // The recovery path replays the calls alone - nothing re-runs the mutation
        var completion = OperationCompletion.New(null, [NewCall()]);

        completion.Command.Should().BeNull();
        completion.InvalidationCalls.Should().HaveCount(1);
    }

    [Fact]
    public void ToStringNamesTheCommandAndTheCallCount()
    {
        var completion = OperationCompletion.New(new OperationCompletionTest_Command("a"), [NewCall()]);

        completion.ToString().Should().Contain(nameof(OperationCompletionTest_Command));
        completion.ToString().Should().Contain("1 invalidation call(s)");
    }

    [Theory]
    [InlineData(SerializerKind.MemoryPack)]
    [InlineData(SerializerKind.MessagePack)]
    [InlineData(SerializerKind.SystemJson)]
    [InlineData(SerializerKind.NewtonsoftJson)]
    public void RoundTrips(SerializerKind serializerKind)
    {
        var source = OperationCompletion.New(new OperationCompletionTest_Command("a"), [NewCall()]);

        var restored = RoundTrip(source, serializerKind);

        // The inner command is polymorphic, so it travels type-decorated rather than as ICommand
        restored.Command.Should().BeOfType<OperationCompletionTest_Command>()
            .Which.Key.Should().Be("a");
        restored.InvalidationCalls.Should().HaveCount(1);
        restored.InvalidationCalls[0].MethodName.Should().Be("Get:2");
        restored.InvalidationCalls[0].Arguments.Get<int>(0).Should().Be(1);
    }

    [Theory]
    [InlineData(SerializerKind.MemoryPack)]
    [InlineData(SerializerKind.MessagePack)]
    [InlineData(SerializerKind.SystemJson)]
    [InlineData(SerializerKind.NewtonsoftJson)]
    public void RoundTripsWithoutACommand(SerializerKind serializerKind)
    {
        var source = OperationCompletion.New(null, [NewCall()]);

        var restored = RoundTrip(source, serializerKind);

        restored.Command.Should().BeNull();
        restored.InvalidationCalls.Should().HaveCount(1);
    }

    // Private methods

    private static OperationCompletion RoundTrip(OperationCompletion source, SerializerKind serializerKind)
    {
        if (serializerKind.GetDefaultSerializer() is ITextSerializer textSerializer) {
            var text = textSerializer.Write(source);
            return textSerializer.Read<OperationCompletion>(text);
        }

        var serializer = serializerKind.GetDefaultSerializer();
        using var buffer = serializer.Write(source, typeof(OperationCompletion));
        var data = (ReadOnlyMemory<byte>)buffer.WrittenMemory;
        return (OperationCompletion)serializer.Read(data, typeof(OperationCompletion), out _)!;
    }

    private static ServiceCall NewCall()
    {
        var type = typeof(Target);
        var method = MethodInfoExt.GetByRpcStyleName(type, "Get:2");
        return ServiceCall.New(type, method, ArgumentList.New(1, CancellationToken.None));
    }

    // Nested types

    public class Target
    {
        public virtual int Get(int key, CancellationToken cancellationToken = default) => key;
    }
}

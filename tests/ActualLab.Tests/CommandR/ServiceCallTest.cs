using ActualLab.Interception;
using ActualLab.Reflection;
using ActualLab.Interception.Serialization;
using ActualLab.Serialization;
using ActualLab.CommandR.Operations;

namespace ActualLab.Tests.CommandR;

public class ServiceCallTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void NewCapturesTheMethodRpcStyle()
    {
        var invocation = NewInvocation("Add", ArgumentList.New(1, 2, CancellationToken.None));

        invocation.ServiceType.Resolve().Should().Be(typeof(TestTarget));
        // The name carries the parameter count, CancellationToken included
        invocation.MethodName.Should().Be("Add:3");
        invocation.Arguments.Get<int>(0).Should().Be(1);
    }

    [Theory]
    [InlineData(SerializerKind.MemoryPack)]
    [InlineData(SerializerKind.MessagePack)]
    [InlineData(SerializerKind.SystemJson)]
    [InlineData(SerializerKind.NewtonsoftJson)]
    public void RoundTrips(SerializerKind serializerKind)
    {
        var source = NewInvocation("Add", ArgumentList.New(1, 2, CancellationToken.None));
        var restored = RoundTrip(source, serializerKind);

        restored.ServiceType.Should().Be(source.ServiceType);
        restored.MethodName.Should().Be("Add:3");
        // The arguments keep their exact types: the method identity is what makes the
        // ArgumentListType known on this side, exactly as it is for an inbound RPC call
        restored.Arguments.Get<int>(0).Should().Be(1);
        restored.Arguments.Get<int>(1).Should().Be(2);
        // ServiceCall itself compares by reference, so content equality is explicit here
        ServiceCall.ContentEqualityComparer.Instance.Equals(restored, source).Should().BeTrue();
    }

    [Theory]
    [InlineData(SerializerKind.MemoryPack)]
    [InlineData(SerializerKind.MessagePack)]
    [InlineData(SerializerKind.SystemJson)]
    [InlineData(SerializerKind.NewtonsoftJson)]
    public void RoundTripsAPolymorphicArgument(SerializerKind serializerKind)
    {
        // The declared type is object, so the payload has to carry the runtime type too
        var source = NewInvocation("Handle", ArgumentList.New((object)"abc", CancellationToken.None));
        var restored = RoundTrip(source, serializerKind);

        restored.Arguments.Get<object>(0).Should().Be("abc");
    }

    [Fact]
    public void TextSerializersUseArgumentTextAndBinaryOnesUseArgumentData()
    {
        var source = NewInvocation("Add", ArgumentList.New(1, 2, CancellationToken.None));

        var json = SystemJsonSerializer.Default.Write(source);
        Out.WriteLine(json);
        // A text serializer carries the arguments as text rather than as base64-ed bytes
        json.Should().ContainEquivalentOf(nameof(ServiceCall.ArgumentText));
        json.Should().NotContainEquivalentOf(nameof(ServiceCall.ArgumentData));

        var newtonsoftJson = NewtonsoftJsonSerializer.Default.Write(source);
        Out.WriteLine(newtonsoftJson);
        newtonsoftJson.Should().ContainEquivalentOf(nameof(ServiceCall.ArgumentText));
        newtonsoftJson.Should().NotContainEquivalentOf(nameof(ServiceCall.ArgumentData));
    }

    [Fact]
    public void TheDeserializationConstructorMaterializesArguments()
    {
        var source = NewInvocation("Add", ArgumentList.New(1, 2, CancellationToken.None));

        // The payload arrives together with the method identity that decodes it, so there is
        // never a record holding an encoded payload it hasn't decoded
        var restored = new ServiceCall(source.ServiceType, source.MethodName, source.ArgumentData);

        restored.Arguments.Get<int>(0).Should().Be(1);
        restored.Arguments.Get<int>(1).Should().Be(2);
    }

    [Fact]
    public void ARestoredRecordSerializesInEitherFormat()
    {
        var source = NewInvocation("Add", ArgumentList.New(1, 2, CancellationToken.None));

        // Both payload properties are projections of Arguments, so the format a record arrived
        // in doesn't limit the format it can leave in
        var fromBytes = new ServiceCall(source.ServiceType, source.MethodName, source.ArgumentData);
        fromBytes.ArgumentText.Should().Be(source.ArgumentText);

        var fromText = new ServiceCall(source.ServiceType, source.MethodName, source.ArgumentText);
        fromText.ArgumentData.Should().Equal(source.ArgumentData);
    }

    [Fact]
    public void ARecordForAnUnknownServiceTypeStillDeserializes()
    {
        var source = NewInvocation("Add", ArgumentList.New(1, 2, CancellationToken.None));

        // One record naming a type this host lacks - a rolling upgrade, say - must not take the
        // rest of the carrier it travels in with it, so the failure waits for whoever needs it
        var unknown = new ServiceCall(
            new TypeRef("NoSuchService, NoSuchAssembly"), source.MethodName, source.ArgumentData);

        unknown.ServiceType.TryResolve().Should().BeNull();
        unknown.MethodName.Should().Be(source.MethodName);
        // The applier drops it by ServiceType and logs it, so ToString() has to stay safe
        unknown.ToString().Should().Contain("undecodable");
        Assert.ThrowsAny<Exception>(() => unknown.Arguments);
    }

    [Fact]
    public void EqualityIsByReference()
    {
        var a = NewInvocation("Add", ArgumentList.New(1, 2, CancellationToken.None));
        var b = NewInvocation("Add", ArgumentList.New(1, 2, CancellationToken.None));

        // Comparing by content would materialize Arguments on both sides
        a.Should().NotBe(b);
        a.Should().Be(a);
    }

    [Fact]
    public void ContentEqualityComparerComparesWhatIsInvoked()
    {
        var a = NewInvocation("Add", ArgumentList.New(1, 2, CancellationToken.None));
        var b = NewInvocation("Add", ArgumentList.New(1, 2, CancellationToken.None));
        var c = NewInvocation("Add", ArgumentList.New(1, 3, CancellationToken.None));
        var comparer = ServiceCall.ContentEqualityComparer.Instance;

        // Content equality for callers that need it - the record itself compares by reference
        comparer.Equals(a, b).Should().BeTrue();
        comparer.GetHashCode(a).Should().Be(comparer.GetHashCode(b));
        comparer.Equals(a, c).Should().BeFalse();
    }

    [Fact]
    public void WithDoesNotCarryOverTheEncodedPayload()
    {
        var source = NewInvocation("Add", ArgumentList.New(1, 2, CancellationToken.None));

        // Add:3 and Handle:2 have different argument lists, so the copy has to carry the new
        // ones rather than anything derived from the old
        var method = MethodInfoExt.GetByRpcStyleName(typeof(TestTarget), "Handle:2");
        var copy = source with {
            MethodName = method.GetRpcStyleName(),
            Arguments = ArgumentList.New((object)"abc", CancellationToken.None),
        };

        copy.MethodName.Should().Be("Handle:2");
        copy.Arguments.Get<object>(0).Should().Be("abc");
        copy.ToString().Should().Contain("Handle:2");
        // And the source is untouched
        source.Arguments.Get<int>(0).Should().Be(1);
    }

    [Fact]
    public void WithKeepsTheArgumentsOfARestoredRecord()
    {
        var source = NewInvocation("Add", ArgumentList.New(1, 2, CancellationToken.None));
        var fromTheWire = new ServiceCall(source.ServiceType, source.MethodName, source.ArgumentData);

        var copy = fromTheWire with { };

        copy.Arguments.Get<int>(0).Should().Be(1);
        copy.Arguments.Get<int>(1).Should().Be(2);
    }

    // Private methods

    private static ServiceCall NewInvocation(string name, ArgumentList arguments)
    {
        var type = typeof(TestTarget);
        var method = MethodInfoExt.GetByRpcStyleName(type, $"{name}:{arguments.Length}");
        return ServiceCall.New(type, method, arguments);
    }

    private static ServiceCall RoundTrip(ServiceCall source, SerializerKind serializerKind)
    {
        if (serializerKind.GetDefaultSerializer() is ITextSerializer textSerializer) {
            var text = textSerializer.Write(source);
            return textSerializer.Read<ServiceCall>(text);
        }

        var serializer = serializerKind.GetDefaultSerializer();
        using var buffer = serializer.Write(source, typeof(ServiceCall));
        var data = (ReadOnlyMemory<byte>)buffer.WrittenMemory;
        return (ServiceCall)serializer.Read(data, typeof(ServiceCall), out _)!;
    }

    [Theory]
    [InlineData(SerializerKind.MemoryPack)]
    [InlineData(SerializerKind.MessagePack)]
    [InlineData(SerializerKind.SystemJson)]
    [InlineData(SerializerKind.NewtonsoftJson)]
    public void RoundTripsAProtectedMethod(SerializerKind serializerKind)
    {
        // A compute method can be protected, and then it isn't on the service's interface - but
        // the record still carries the registered service type, because that's the DI key the
        // applier resolves the service by
        var method = MethodInfoExt.GetByRpcStyleName(typeof(TestTarget), "AddInternal:3");
        var source = ServiceCall.New(
            typeof(ITestTarget), method, ArgumentList.New(1, 2, CancellationToken.None));
        var restored = RoundTrip(source, serializerKind);

        restored.Arguments.Get<int>(0).Should().Be(1);
        restored.Arguments.Get<int>(1).Should().Be(2);
    }

    // Nested types

    public interface ITestTarget
    {
        public int Add(int a, int b, CancellationToken cancellationToken = default);
    }

    public class TestTarget : ITestTarget
    {
        public virtual int Add(int a, int b, CancellationToken cancellationToken = default)
            => a + b;

        // Not on ITestTarget - the case a service's own compute method can be in
        protected virtual int AddInternal(int a, int b, CancellationToken cancellationToken = default)
            => a + b;

        // Its declared parameter type is polymorphic, unlike Add's
        public virtual string Handle(object value, CancellationToken cancellationToken = default)
            => value.ToString() ?? "";
    }
}

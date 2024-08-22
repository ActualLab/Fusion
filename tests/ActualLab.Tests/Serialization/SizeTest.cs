using System.Reflection;
using ActualLab.Reflection;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Tests.Serialization;

#if NETCOREAPP3_1_OR_GREATER

public class SizeTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Theory]
    [InlineData(typeof(bool), 1)]
    [InlineData(typeof(Moment), 8)]
    [InlineData(typeof(Moment?), 16, 9)]
    [InlineData(typeof(Option<Moment>), 16)]
    [InlineData(typeof(ApiNullable<Moment>), 9)]
    [InlineData(typeof(ApiNullable8<Moment>), 16)]
    [InlineData(typeof(byte?), 2)]
    [InlineData(typeof(Option<byte>), 2)]
    [InlineData(typeof(short?), 4)]
    [InlineData(typeof(Option<short>), 4)]
    [InlineData(typeof(int?), 8)]
    [InlineData(typeof(Option<int>), 8)]
    [InlineData(typeof(long?), 16)]
    [InlineData(typeof(Option<long>), 16)]
    [InlineData(typeof(Guid?), 20)]
    [InlineData(typeof(Option<Guid>), 24, 20)]
    [InlineData(typeof(Unit), 1)]
    [InlineData(typeof(Unit?), 2)]
    [InlineData(typeof(Option<Unit>), 2)]
    [InlineData(typeof(RpcNoWait), 1)]
    [InlineData(typeof(RpcNoWait?), 2)]
    [InlineData(typeof(Option<RpcNoWait>), 2)]
    [InlineData(typeof(RpcObjectId), 24)]
    public void BasicTest(Type type, int size, int? altSize = null)
    {
        var testMethod = GetType().GetMethod(nameof(Test), BindingFlags.Instance | BindingFlags.NonPublic)!;
        testMethod.MakeGenericMethod(type).Invoke(this, [size, altSize]);
    }

    private void Test<T>(int expectedSize, int? expectedAltSize)
    {
        var size = Unsafe.SizeOf<T>();
        Out.WriteLine($"Size of {typeof(T).GetName()} = {size}");
        if (size == expectedSize)
            return;

        if (expectedAltSize is { } v)
            size.Should().Be(v);
        else
            size.Should().Be(expectedSize);
    }
}

#endif

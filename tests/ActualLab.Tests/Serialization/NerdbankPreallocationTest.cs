#if NET8_0_OR_GREATER
using System.Buffers.Binary;

namespace ActualLab.Tests.Serialization;

/// <summary>
/// A msgpack collection header is one wire byte per ~1 declared item, but a reference array
/// costs 8 bytes per slot - so a converter that sizes its buffer from the header hands a peer
/// an 8x allocation amplifier. These tests keep the Nerdbank converters growing incrementally.
/// </summary>
public class NerdbankPreallocationTest(ITestOutputHelper @out) : TestBase(@out)
{
    private const int DeclaredCount = 4_000_000;
    private const long MaxAllocatedBytes = 2L << 20;

    [Fact]
    public void ApiArrayShouldNotPreallocateFromDeclaredCount()
    {
        NbRead<ApiArray<string>>(NbWrite(ApiArray.New("warmup")));

        var payload = new byte[5 + DeclaredCount];
        WriteArray32Header(payload.AsSpan(), DeclaredCount);

        AssertFailsWithoutAllocating<ApiArray<string>>(payload);
    }

    [Fact]
    public void PropertyBagShouldNotPreallocateFromDeclaredCount()
    {
        NbRead<PropertyBag>(NbWrite(PropertyBag.Empty.Set("warmup", 1)));

        var payload = new byte[1 + 5 + DeclaredCount];
        payload[0] = 0x91; // fixarray(1) - the wrapper around RawItems
        WriteArray32Header(payload.AsSpan(1), DeclaredCount);

        AssertFailsWithoutAllocating<PropertyBag>(payload);
    }

    // Private methods

    private void AssertFailsWithoutAllocating<T>(byte[] payload)
    {
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        try {
            NbRead<T>(payload);
            Assert.Fail($"{typeof(T).Name} deserialization was expected to fail.");
        }
        catch (Exception e) when (e is not Xunit.Sdk.XunitException) {
            Out.WriteLine($"{typeof(T).Name}: {e.GetType().Name}: {e.Message}");
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Out.WriteLine($"{typeof(T).Name}: allocated {allocated} bytes");
        allocated.Should().BeLessThan(MaxAllocatedBytes);
    }

    private static void WriteArray32Header(Span<byte> target, int count)
    {
        target[0] = 0xdd; // array32
        BinaryPrimitives.WriteUInt32BigEndian(target[1..], (uint)count);
    }

    private static byte[] NbWrite<T>(T value)
    {
        var s = new NerdbankMessagePackByteSerializer().ToTyped<T>();
        using var buffer = s.Write(value);
        return buffer.WrittenMemory.ToArray();
    }

    private static T NbRead<T>(byte[] bytes)
    {
        var s = new NerdbankMessagePackByteSerializer().ToTyped<T>();
        return s.Read(bytes, out _);
    }
}
#endif

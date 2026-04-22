namespace ActualLab.Tests.Serialization;

/// <summary>
/// Explicit wire-level cross-compat tests for the Nerdbank.MessagePack converters
/// registered in ActualLab.Serialization.NerdbankMessagePack — does a payload produced by
/// one serializer decode to an equal value through the other?
/// <para>
/// Each test pair drives the bytes directly: <c>MessagePack-CSharp bytes → Nerdbank read</c>
/// and <c>Nerdbank bytes → MessagePack-CSharp read</c>. Value-equality is asserted on both
/// sides. Byte-equality is asserted where both serializers happen to produce the same
/// canonical wire (ApiMap empty, ImmutableOptionSet empty); otherwise only the semantic
/// round-trip is checked.
/// </para>
/// <para>
/// <see cref="TypeDecoratingUniSerialized{T}"/> is cross-compat too: the Nerdbank converter
/// deliberately mirrors MessagePack-CSharp's <c>[Key(0)] MessagePackData</c> layout (a
/// 1-element array wrapping a <c>bin</c> of type-decorated inner bytes), and the inner bytes
/// use the shared msgpack wire format so they come out byte-identical for the cases covered
/// here. See <see cref="TypeDecoratingUniSerialized_CrossCompat"/>.
/// </para>
/// </summary>
public class NerdbankCrossCompatTest(ITestOutputHelper @out) : TestBase(@out)
{
    // --- ApiMap<TKey, TValue> -----------------------------------------------------------

    [Fact]
    public void ApiMap_Empty_CrossCompat()
    {
        var empty = new ApiMap<string, int>();
        AssertBytesCrossDecode(empty, (a, b) => a.Should().BeEquivalentTo(b));
    }

    [Fact]
    public void ApiMap_Populated_CrossCompat()
    {
        var map = new ApiMap<string, int> {
            ["alpha"] = 1,
            ["bravo"] = 2,
            ["charlie"] = 3,
        };
        AssertBytesCrossDecode(map, (a, b) => a.Should().BeEquivalentTo(b));
    }

    [Fact]
    public void ApiMap_IntKey_CrossCompat()
    {
        var map = new ApiMap<int, string> {
            [1] = "one",
            [2] = "two",
        };
        AssertBytesCrossDecode(map, (a, b) => a.Should().BeEquivalentTo(b));
    }

    // --- ImmutableOptionSet -------------------------------------------------------------

    [Fact]
    public void ImmutableOptionSet_Empty_CrossCompat()
    {
        AssertBytesCrossDecode(
            default(ImmutableOptionSet),
            (a, b) => a.Items.IsEmpty.Should().BeTrue());
    }

    [Fact]
    public void ImmutableOptionSet_Populated_CrossCompat()
    {
        var s = new ImmutableOptionSet()
            .Set(42)
            .Set("hello")
            .Set((1, "tag"));
        AssertBytesCrossDecode(s, (a, b) => a.Items.Should().BeEquivalentTo(b.Items));
    }

    // --- PropertyBag --------------------------------------------------------------------
    //
    // PropertyBag wraps entries in TypeDecoratingUniSerialized<object>, so its cross-compat
    // reduces to TypeDecoratingUniSerialized's cross-compat — which the Nerdbank converter
    // now preserves by emitting MessagePack-CSharp's [Key(0)] MessagePackData layout.

    [Fact]
    public void PropertyBag_Empty_CrossCompat()
    {
        AssertBytesCrossDecode(
            default(PropertyBag),
            (a, b) => a.Count.Should().Be(b.Count));
    }

    [Fact]
    public void PropertyBag_Populated_CrossCompat()
    {
        var bag = new PropertyBag().Set("greeting", "hello").Set("count", 42);
        AssertBytesCrossDecode(bag, (a, b) => {
            a.Get<string>("greeting").Should().Be(b.Get<string>("greeting"));
            a.Get<int>("count").Should().Be(b.Get<int>("count"));
        });
    }

    // --- TypeDecoratingUniSerialized<T> -------------------------------------------------

    [Fact]
    public void TypeDecoratingUniSerialized_CrossCompat()
    {
        // Wire format is intentionally aligned with MessagePack-CSharp's [Key(0)] MessagePackData
        // layout — a 1-element array carrying the type-decorated inner bytes as a bin. Since the
        // inner payload uses the shared msgpack wire format (string for TypeRef, default converter
        // for the value), bytes come out identical and cross-reads round-trip cleanly.
        var value = TypeDecoratingUniSerialized.New<object>("hello");
        var mpBytes = MpWrite(value);
        var nbBytes = NbWrite(value);
        @out.WriteLine($"MP ({mpBytes.Length}): {Convert.ToHexString(mpBytes)}");
        @out.WriteLine($"NB ({nbBytes.Length}): {Convert.ToHexString(nbBytes)}");

        // Byte-identical wire is the whole point of this converter's design.
        nbBytes.Should().BeEquivalentTo(mpBytes, "the Nerdbank converter is designed to be byte-identical to MessagePack-CSharp's [Key(0)] MessagePackData layout");

        // Self round-trips.
        MpRead<TypeDecoratingUniSerialized<object>>(mpBytes).Value.Should().Be("hello");
        NbRead<TypeDecoratingUniSerialized<object>>(nbBytes).Value.Should().Be("hello");

        // Cross-reads succeed.
        NbRead<TypeDecoratingUniSerialized<object>>(mpBytes).Value.Should().Be("hello");
        MpRead<TypeDecoratingUniSerialized<object>>(nbBytes).Value.Should().Be("hello");
    }

    // --- Helpers ------------------------------------------------------------------------

    private void AssertBytesCrossDecode<T>(T value, Action<T, T> assert)
    {
        var mpBytes = MpWrite(value);
        var nbBytes = NbWrite(value);
        @out.WriteLine($"{typeof(T).Name} MP ({mpBytes.Length}): {Convert.ToHexString(mpBytes)}");
        @out.WriteLine($"{typeof(T).Name} NB ({nbBytes.Length}): {Convert.ToHexString(nbBytes)}");

        // Self round-trips.
        var mpSelf = MpRead<T>(mpBytes);
        assert(mpSelf, value);
        var nbSelf = NbRead<T>(nbBytes);
        assert(nbSelf, value);

        // Cross decodes: MP-written bytes → NB reader, and NB-written bytes → MP reader.
        var crossMpToNb = NbRead<T>(mpBytes);
        assert(crossMpToNb, value);
        var crossNbToMp = MpRead<T>(nbBytes);
        assert(crossNbToMp, value);
    }

    private static byte[] MpWrite<T>(T value)
    {
        var s = new MessagePackByteSerializer().ToTyped<T>();
        using var buffer = s.Write(value);
        return buffer.WrittenMemory.ToArray();
    }

    private static T MpRead<T>(byte[] bytes)
    {
        var s = new MessagePackByteSerializer().ToTyped<T>();
        return s.Read(bytes, out _);
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

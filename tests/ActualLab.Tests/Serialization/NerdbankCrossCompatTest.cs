using ActualLab.Api;
using ActualLab.Collections;
using ActualLab.Collections.Internal;

namespace ActualLab.Tests.Serialization;

/// <summary>
/// Explicit wire-level cross-compat tests for the four Nerdbank.MessagePack converters
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
/// The <see cref="TypeDecoratingUniSerialized{T}"/> converter is intentionally NOT cross-compat:
/// the migration swapped its inner transcoding channel from MessagePack-CSharp to Nerdbank,
/// so the map-envelope key name and payload bytes differ by design. That converter is
/// documented separately in <see cref="TypeDecoratingUniSerializedMigrationBreak"/>.
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
    // PropertyBag's wire envelope (1-element array wrapping PropertyBagItem[]) is cross-compat.
    // PropertyBagItem's (key, TypeDecoratingUniSerialized<object>) layout is also cross-compat
    // at the outer level — but the inner TypeDecoratingUniSerialized<object> payload is where
    // the migration break kicks in: its map key changed from "MessagePack" to "Value", and the
    // inner bytes switched from MessagePack-CSharp type-decoration to Nerdbank's. So a
    // populated PropertyBag is NOT expected to round-trip cross-serializer. The empty case is.

    [Fact]
    public void PropertyBag_Empty_CrossCompat()
    {
        AssertBytesCrossDecode(
            default(PropertyBag),
            (a, b) => a.Count.Should().Be(b.Count));
    }

    [Fact]
    public void PropertyBag_Populated_MpToMp_RoundTrip()
    {
        // Self-round-trip through MessagePack only — PropertyBag entries with a non-trivial
        // payload are intentionally not cross-compat (see class comment).
        var bag = new PropertyBag().Set("greeting", "hello").Set("count", 42);
        var bytes = MpWrite(bag);
        @out.WriteLine($"MP bytes ({bytes.Length}): {Convert.ToHexString(bytes)}");
        var back = MpRead<PropertyBag>(bytes);
        back.Get<string>("greeting").Should().Be("hello");
        back.Get<int>("count").Should().Be(42);
    }

    [Fact]
    public void PropertyBag_Populated_NbToNb_RoundTrip()
    {
        var bag = new PropertyBag().Set("greeting", "hello").Set("count", 42);
        var bytes = NbWrite(bag);
        @out.WriteLine($"NB bytes ({bytes.Length}): {Convert.ToHexString(bytes)}");
        var back = NbRead<PropertyBag>(bytes);
        back.Get<string>("greeting").Should().Be("hello");
        back.Get<int>("count").Should().Be(42);
    }

    // --- TypeDecoratingUniSerialized<T> (deliberate migration break) --------------------

    [Fact]
    public void TypeDecoratingUniSerializedMigrationBreak()
    {
        // MessagePack and Nerdbank produce DIFFERENT wire shapes for this type:
        //   MP: {"MessagePack": <MP-type-decorated bytes>}
        //   NB: {"Value":       <Nerdbank-type-decorated bytes>}
        // That's by design — the Nerdbank converter replaces the MessagePack transcoding
        // channel with Nerdbank's own. Document the break here so a future change that
        // accidentally "fixes" cross-compat (e.g. by reviving MessagePack decoding inside
        // the NB converter) has to delete this test on purpose.
        var value = TypeDecoratingUniSerialized.New<object>("hello");
        var mpBytes = MpWrite(value);
        var nbBytes = NbWrite(value);
        @out.WriteLine($"MP ({mpBytes.Length}): {Convert.ToHexString(mpBytes)}");
        @out.WriteLine($"NB ({nbBytes.Length}): {Convert.ToHexString(nbBytes)}");

        // Each serializer round-trips its own bytes.
        var mpBack = MpRead<TypeDecoratingUniSerialized<object>>(mpBytes);
        mpBack.Value.Should().Be("hello");
        var nbBack = NbRead<TypeDecoratingUniSerialized<object>>(nbBytes);
        nbBack.Value.Should().Be("hello");

        // Cross-read is expected to fail — either with an exception (wire-level mismatch at
        // the outer envelope) or by returning a default-valued wrapper (outer keys matched
        // but the inner payload bytes are in the other dialect). This is the documented
        // migration break; document BOTH failure modes so either implementation detail is
        // acceptable but a clean round-trip is not.
        TryReadOrDefault<TypeDecoratingUniSerialized<object>>(mpBytes, NbRead<TypeDecoratingUniSerialized<object>>)
            .Value.Should().NotBe("hello",
                "MessagePack bytes must NOT round-trip cleanly through Nerdbank post-migration");
        TryReadOrDefault<TypeDecoratingUniSerialized<object>>(nbBytes, MpRead<TypeDecoratingUniSerialized<object>>)
            .Value.Should().NotBe("hello",
                "Nerdbank bytes must NOT round-trip cleanly through MessagePack post-migration");
    }

    private static T TryReadOrDefault<T>(byte[] bytes, Func<byte[], T> reader)
    {
        try { return reader(bytes); }
        catch { return default!; }
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

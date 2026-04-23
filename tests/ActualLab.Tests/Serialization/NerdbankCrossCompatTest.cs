using ActualLab.Rpc;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Infrastructure;

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
        Out.WriteLine($"MP ({mpBytes.Length}): {Convert.ToHexString(mpBytes)}");
        Out.WriteLine($"NB ({nbBytes.Length}): {Convert.ToHexString(nbBytes)}");

        // Byte-identical wire is the whole point of this converter's design.
        nbBytes.Should().BeEquivalentTo(mpBytes, "the Nerdbank converter is designed to be byte-identical to MessagePack-CSharp's [Key(0)] MessagePackData layout");

        // Self round-trips.
        MpRead<TypeDecoratingUniSerialized<object>>(mpBytes).Value.Should().Be("hello");
        NbRead<TypeDecoratingUniSerialized<object>>(nbBytes).Value.Should().Be("hello");

        // Cross-reads succeed.
        NbRead<TypeDecoratingUniSerialized<object>>(mpBytes).Value.Should().Be("hello");
        MpRead<TypeDecoratingUniSerialized<object>>(nbBytes).Value.Should().Be("hello");
    }

    // --- System RPC types ----------------------------------------------------------------
    //
    // Each of these types ships a custom Nerdbank converter that mirrors the MessagePack-CSharp
    // [Key(N)] array layout. The wire is intentionally byte-identical so the .NET server speaks
    // the same protocol the TS RPC client emits (which uses MessagePack-CSharp's binary format).

    [Fact]
    public void RpcHandshake_CrossCompat()
    {
        var handshake = new RpcHandshake(
            RemotePeerId: Guid.Parse("12345678-1234-1234-1234-123456789012"),
            RemoteApiVersionSet: new VersionSet("api", "1.2.3"),
            RemoteHubId: Guid.Parse("87654321-4321-4321-4321-210987654321"),
            ProtocolVersion: RpcHandshake.CurrentProtocolVersion,
            Index: 7);
        AssertBytesCrossDecode<RpcHandshake?>(handshake, AssertHandshake);

        // Also exercise the null version-set path, which is what the TS client emits.
        var withoutVersions = handshake with { RemoteApiVersionSet = null };
        AssertBytesCrossDecode<RpcHandshake?>(withoutVersions, AssertHandshake);

        static void AssertHandshake(RpcHandshake? a, RpcHandshake? b)
        {
            a.Should().NotBeNull();
            b.Should().NotBeNull();
            a!.RemotePeerId.Should().Be(b!.RemotePeerId);
            a.RemoteHubId.Should().Be(b.RemoteHubId);
            a.ProtocolVersion.Should().Be(b.ProtocolVersion);
            a.Index.Should().Be(b.Index);
            (a.RemoteApiVersionSet?.Value ?? "").Should().Be(b.RemoteApiVersionSet?.Value ?? "");
        }
    }

    [Fact]
    public void VersionSet_CrossCompat()
    {
        AssertBytesCrossDecode<VersionSet?>(
            new VersionSet("api", "1.2.3"),
            (a, b) => a!.Value.Should().Be(b!.Value));
        AssertBytesCrossDecode<VersionSet?>(
            VersionSet.Empty,
            (a, b) => a!.Value.Should().Be(b!.Value));
    }

    [Fact]
    public void RpcObjectId_CrossCompat()
    {
        var id = new RpcObjectId(Guid.Parse("11111111-2222-3333-4444-555555555555"), 42);
        AssertBytesCrossDecode(id, (a, b) => a.Should().Be(b));
        AssertBytesCrossDecode(default(RpcObjectId), (a, b) => a.Should().Be(b));
    }

    [Fact]
    public void RpcHeader_CrossCompat()
    {
        AssertBytesCrossDecode(
            new RpcHeader("Content-Type", "application/octet-stream"),
            (a, b) => {
                a.Name.Should().Be(b.Name);
                a.Value.Should().Be(b.Value);
            });
        AssertBytesCrossDecode(
            new RpcHeader("Empty", ""),
            (a, b) => {
                a.Name.Should().Be(b.Name);
                a.Value.Should().Be(b.Value);
            });
    }

    [Fact]
    public void RpcHeaderKey_CrossCompat()
    {
        var key = new RpcHeaderKey("X-Trace-Id");
        AssertBytesCrossDecode(key, (a, b) => a.Name.Should().Be(b.Name));
    }

    [Fact]
    public void RpcMethodRef_CrossCompat()
    {
        var methodRef = new RpcMethodRef("MyService.MyMethod");
        AssertBytesCrossDecode(methodRef, (a, b) => a.Name.Should().Be(b.Name));
    }

    [Fact]
    public void RpcCacheKey_CrossCompat()
    {
        var key = new RpcCacheKey("MyService.MyMethod", new byte[] { 1, 2, 3, 4, 5 });
        AssertBytesCrossDecode<RpcCacheKey?>(key, (a, b) => a.Should().Be(b));
    }

    [Fact]
    public void RpcCacheValue_CrossCompat()
    {
        var value = new RpcCacheValue(new byte[] { 9, 8, 7 }, "deadbeef");
        AssertBytesCrossDecode<RpcCacheValue?>(value, (a, b) => a.Should().Be(b));
        AssertBytesCrossDecode<RpcCacheValue?>(
            new RpcCacheValue(default, ""),
            (a, b) => a.Should().Be(b));
    }

    [Fact]
    public void Result_CrossCompat()
    {
        AssertBytesCrossDecode(
            new Result<int>(42, null),
            (a, b) => {
                a.HasValue.Should().Be(b.HasValue);
                a.ValueOrDefault.Should().Be(b.ValueOrDefault);
            });
        AssertBytesCrossDecode(
            new Result<int>(0, new InvalidOperationException("boom")),
            (a, b) => {
                a.HasError.Should().Be(b.HasError);
                a.Error!.GetType().Should().Be(b.Error!.GetType());
                a.Error.Message.Should().Be(b.Error.Message);
            });
        AssertBytesCrossDecode(
            new Result<string>("hello", null),
            (a, b) => a.ValueOrDefault.Should().Be(b.ValueOrDefault));
    }

    [Fact]
    public void ExceptionInfo_CrossCompat()
    {
        // ExceptionInfo uses [MessagePackObject(true)] (string-keyed map). It has no custom Nerdbank
        // converter — Nerdbank's reflection-based reader/writer produces and consumes a name-keyed
        // map by default, matching MP-CSharp's wire format. This test guards against drift.
        var info = new ExceptionInfo(new InvalidOperationException("boom"));
        AssertBytesCrossDecode(info, (a, b) => {
            a.TypeRef.Should().Be(b.TypeRef);
            a.Message.Should().Be(b.Message);
        });
        AssertBytesCrossDecode(default(ExceptionInfo), (a, b) => a.Should().Be(b));
    }

    // --- Helpers ------------------------------------------------------------------------

    private void AssertBytesCrossDecode<T>(T value, Action<T, T> assert, bool requireByteEquality = true)
    {
        var mpBytes = MpWrite(value);
        var nbBytes = NbWrite(value);
        Out.WriteLine($"{typeof(T).Name} MP ({mpBytes.Length}): {Convert.ToHexString(mpBytes)}");
        Out.WriteLine($"{typeof(T).Name} NB ({nbBytes.Length}): {Convert.ToHexString(nbBytes)}");

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

        // Byte-identical wire: the converters are designed to match MessagePack-CSharp's layout
        // exactly, so both serializers should emit the same bytes for the same value. Opt-out is
        // provided for the few cases where independent map orderings legitimately diverge.
        if (requireByteEquality)
            nbBytes.Should().Equal(mpBytes,
                "Nerdbank wire must be byte-identical to MessagePack-CSharp for cross-runtime compatibility");
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

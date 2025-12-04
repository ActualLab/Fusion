using ActualLab.Collections.Internal;
using ActualLab.IO;
using ActualLab.Reflection;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using TextOrBytes = ActualLab.Serialization.TextOrBytes;

namespace ActualLab.Tests.Serialization;

public class SerializationTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void ExceptionInfoSerialization()
    {
        var n = default(ExceptionInfo);
        n.ToException().Should().BeNull();
        n = new ExceptionInfo(null!);
        n = n.AssertPassesThroughAllSerializers(Out);
        n.ToException().Should().BeNull();

        var e = new InvalidOperationException("Fail!");
        var p = e.ToExceptionInfo();
        p = p.AssertPassesThroughAllSerializers(Out);
        var e1 = p.ToException();
        e1.Should().BeOfType<InvalidOperationException>();
        e1!.Message.Should().Be(e.Message);
    }

    [Fact]
    public void TypeDecoratingSerializerTest()
    {
        var serializer = TypeDecoratingTextSerializer.Default;

        var value = new Tuple<DateTime>(DateTime.Now);
        var json = serializer.Write<object>(value);
        WriteLine(json);

        var deserialized = (Tuple<DateTime>)serializer.Read<object>(json);
        deserialized.Item1.Should().Be(value.Item1);
    }

    [Fact]
    public void UnitSerialization()
    {
        default(Unit).AssertPassesThroughAllSerializers(Out);
    }

    [Fact]
    public void MomentSerialization()
    {
        default(Moment).AssertPassesThroughAllSerializers(Out);
        Moment.EpochStart.AssertPassesThroughAllSerializers(Out);
        Moment.Now.AssertPassesThroughAllSerializers(Out);
        new Moment(DateTime.MinValue.ToUniversalTime()).AssertPassesThroughAllSerializers(Out);
        new Moment(DateTime.MaxValue.ToUniversalTime()).AssertPassesThroughAllSerializers(Out);
    }

    [Fact]
    public void TypeRefSerialization()
    {
        default(TypeRef).AssertPassesThroughAllSerializers(Out);
        new TypeRef(typeof(bool)).AssertPassesThroughAllSerializers(Out);
    }

    [Fact]
    public void ResultSerialization()
    {
        default(Result<int>).AssertPassesThroughAllSerializers(Out);
        Result.New(1).AssertPassesThroughAllSerializers(Out);
        var r = Result.NewError<int>(new InvalidOperationException()).PassThroughAllSerializers();
        r.Error.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void SymbolSerialization()
    {
        default(Symbol).AssertPassesThroughAllSerializers(Out);
        Symbol.Empty.AssertPassesThroughAllSerializers(Out);
        new Symbol(null!).AssertPassesThroughAllSerializers(Out);
        new Symbol("").AssertPassesThroughAllSerializers(Out);
        new Symbol("1234").AssertPassesThroughAllSerializers(Out);
    }

    [Fact]
    public void VersionSetSerialization()
    {
        Test(null);
        Test(new VersionSet());
        Test(new VersionSet(("", new Version())));
        Test(new VersionSet(("X", "1.0"), ("Y", "1.1.1")));

        Assert.Throws<ArgumentException>(() => new VersionSet(("X", "A")));
        Assert.Throws<ArgumentException>(() => new VersionSet(("X", "1"), ("X", "2")));

        void Test(VersionSet? s) {
            WriteLine(s?.ToString() ?? "null");
            var hs = s.PassThroughAllSerializers();
            hs.Should().Be(s);
        }
    }

    [Fact]
    public void RpcHeaderSerialization()
    {
        Test(default);
        Test(new RpcHeader("a"));
        Test(new RpcHeader("a", "b"));
        Test(new RpcHeader("", "b"));
        Test(new RpcHeader("xxx", "yyy"));

        void Test(RpcHeader h) {
            var hs = h.PassThroughAllSerializers();
            hs.Name.Should().Be(h.Name);
            hs.Value.Should().Be(h.Value);
        }
    }

    [Fact]
    public void RpcHandshakeSerialization()
    {
        Test(new RpcHandshake(default, null, default, 0, 0));
        Test(new RpcHandshake(default, null, default, 1, 1));

        var hs = new RpcHandshake(new Guid(), new VersionSet(("Test", "1.0")), new Guid(), 2, 2);
        Test(hs);

        // Old RpcHandshake -> new RpcHandshake test
        var ohs = new OldRpcHandshake(hs.RemotePeerId, hs.RemoteApiVersionSet, hs.RemoteHubId);
        ohs.AssertPassesThroughAllSerializers<OldRpcHandshake, RpcHandshake>(AssertEqual);

        static void Test(RpcHandshake h) {
            var hs = h.PassThroughAllSerializers();
            hs.Should().Be(h);
        }

        static void AssertEqual(RpcHandshake value, OldRpcHandshake expected) {
            value.RemotePeerId.Should().Be(expected.RemotePeerId);
            value.RemoteHubId.Should().Be(expected.RemoteHubId);
            value.RemoteApiVersionSet!.Value.Should().Be(expected.RemoteApiVersionSet!.Value);
        }
    }

    [Fact]
    public void RpcNoWaitSerialization()
    {
        var x = default(RpcNoWait);
        var o = default(OldRpcNoWait);

        WriteLine("New:");
        x.PassThroughAllSerializers(Out);
        WriteLine("Old:");
        o.PassThroughAllSerializers(Out);

        WriteLine("Old to new:");
        o.AssertPassesThroughAllSerializers<OldRpcNoWait, RpcNoWait>((_, _) => {}, Out);
        WriteLine("New to old:");
        x.AssertPassesThroughAllSerializers<RpcNoWait, OldRpcNoWait>((_, _) => {}, Out);
    }

    [Fact]
    public void RpcObjectIdSerialization()
    {
        Test(default);
        Test(new RpcObjectId(new Guid(), 2));

        void Test(RpcObjectId o) {
            var os = o.PassThroughAllSerializers();
            os.HostId.Should().Be(o.HostId);
            os.LocalId.Should().Be(o.LocalId);
        }
    }

    [Fact]
    public void RpcMessageV1Serialization()
    {
        Test(new RpcMessageV1(0, 3, "s", "m",
            new TextOrBytes([1, 2, 3]),
            null));

        Test(new RpcMessageV1(1, 3, "s", "m",
            new TextOrBytes([1, 2, 3]),
            []));

        Test(new RpcMessageV1(2, 3, "s", "m",
            new TextOrBytes([1, 2, 3]),
            [
                new("v", "@OVhtp0TRc")
            ]));

        Test(new RpcMessageV1(0, 3, "s", "m",
            new TextOrBytes([1, 2, 3]),
            [
                new("a", "b"),
                new("v", "@OVhtp0TRc")
            ]));

        void Test(RpcMessageV1 m) {
            var ms = m.PassThroughAllSerializers();
            ms.RelatedId.Should().Be(m.RelatedId);
            ms.Service.Should().Be(m.Service);
            ms.Method.Should().Be(m.Method);
            ms.ArgumentData.Data.ToArray().Should().Equal(m.ArgumentData.Data.ToArray());
            ms.Headers?.Length.Should().Be(m.Headers?.Length);
            foreach (var (hs, h) in ms.Headers.OrEmpty().Zip(m.Headers.OrEmpty(), (hs, h) => (hs, h))) {
                hs.Name.Should().Be(h.Name);
                hs.Value.Should().Be(h.Value);
            }
        }
    }

    [Fact]
    public void FilePathSerialization()
    {
        default(FilePath).AssertPassesThroughAllSerializers(Out);
        FilePath.Empty.AssertPassesThroughAllSerializers(Out);
        FilePath.New("C:\\").AssertPassesThroughAllSerializers(Out);
    }

    [Fact]
    public void JsonStringSerialization()
    {
        default(JsonString).AssertPassesThroughAllSerializers(Out);
        JsonString.Null.AssertPassesThroughAllSerializers(Out);
        JsonString.Empty.AssertPassesThroughAllSerializers(Out);
        new JsonString("1").AssertPassesThroughAllSerializers(Out);
        new JsonString("12").AssertPassesThroughAllSerializers(Out);
    }

    [Fact]
    public void ByteStringSerialization()
    {
        var e0 = Test(default);
        var e1 = Test(ByteString.Empty);
        var e2 = Test(new ByteString(null!));
        var e3 = Test(new ByteString([]));
        e3.Should().Be(e0).And.Be(e1).And.Be(e2);
        e3.GetHashCode().Should().Be(e0.GetHashCode()).And.Be(e1.GetHashCode()).And.Be(e2.GetHashCode());

        var s1 = Test(new ByteString([1]));
        s1.Should().NotBe(e0);
        s1.GetHashCode().Should().NotBe(e0.GetHashCode());

        var s2 = Test(new ByteString([1, 2]));
        s2.Should().NotBe(e0);
        s2.GetHashCode().Should().NotBe(e0.GetHashCode());

        var s3 = Test(new ByteString(Enumerable.Range(0, 500).Select(i => (byte)i).ToArray()));
        s3.Should().NotBe(e0);
        s3.GetHashCode().Should().NotBe(e0.GetHashCode());

        ByteString Test(ByteString src) {
            var dst = src.PassThroughAllSerializers(Out);
            src.GetHashCode().Equals(dst.GetHashCode()).Should().BeTrue();
            src.Equals(dst).Should().BeTrue();
            src.Span.SequenceEqual(dst.Span).Should().BeTrue();
            return src;
        }
    }

    [Fact]
    public void TextOrBytesSerialization()
    {
        Test(default);

        Test(TextOrBytes.EmptyText);
        Test(new TextOrBytes(""));
        Test(new TextOrBytes("1"));
        Test(new TextOrBytes("2"));

        Test(TextOrBytes.EmptyBytes);
        Test(new TextOrBytes(ByteString.EmptyBytes));
        Test(new TextOrBytes([1]));
        Test(new TextOrBytes([1, 2]));

        void Test(TextOrBytes src) {
            var dst = src.PassThroughAllSerializers(Out);
            dst.Format.Should().Be(src.Format);
            dst.Bytes.SequenceEqual(src.Bytes).Should().BeTrue();
        }
    }

    [Fact]
    public void OptionSerialization()
    {
        default(Option<int>).AssertPassesThroughAllSerializers(Out);
        Option.None<int>().AssertPassesThroughAllSerializers(Out);
        Option.Some(0).AssertPassesThroughAllSerializers(Out);
        Option.Some(1).AssertPassesThroughAllSerializers(Out);
    }

    [Fact]
    public void ImmutableBimapSerialization()
    {
        default(ImmutableBimap<string, string>).AssertPassesThroughAllSerializers(Out);
        var m = new ImmutableBimap<string, int>().PassThroughAllSerializers();
        m.Forward.Count.Should().Be(0);
        m.Backward.Count.Should().Be(0);

        m = new ImmutableBimap<string, int>() {
            Forward = new Dictionary<string, int>() {{ "A", 1 }}
        };
        m.Backward.Count.Should().Be(1);
        m.Backward[1].Should().Be("A");

        var m1 = m.PassThroughAllSerializers(Out);
        m1.Forward.Should().BeEquivalentTo(m.Forward);
        m1.Backward.Should().BeEquivalentTo(m.Backward);
    }

    [Fact]
    public void OptionSetSerialization()
    {
        default(OptionSet).AssertPassesThroughAllSerializers(Out);
        var s = new OptionSet();
        s.Set(3);
        s.Set("X");
        s.Set((1, "X"));
        var s1 = s.PassThroughAllSerializers(Out);
        s1.Items.Should().BeEquivalentTo(s.Items);
    }

    [Fact]
    public void ImmutableOptionSetSerialization()
    {
        default(ImmutableOptionSet).AssertPassesThroughAllSerializers(
            x => x.Items.IsEmpty.Should().BeTrue(),
            Out);
        var s = new ImmutableOptionSet();
        s = s.Set(3);
        s = s.Set("X");
        s = s.Set((1, "X"));
        var s1 = s.PassThroughAllSerializers(Out);
        s1.Items.Should().BeEquivalentTo(s.Items);
    }

    [Fact]
    public void UniSerializedSerialization()
    {
        UniSerialized.New(default(Unit)).AssertPassesThroughAllSerializers(Out);
        UniSerialized.New(1).AssertPassesThroughAllSerializers(Out);
        UniSerialized.New((int?)null).AssertPassesThroughAllSerializers(Out);
        UniSerialized.New((int?)1).AssertPassesThroughAllSerializers(Out);
        UniSerialized.New((string?)null).AssertPassesThroughAllSerializers(Out);
        UniSerialized.New("").AssertPassesThroughAllSerializers(Out);
        UniSerialized.New("A").AssertPassesThroughAllSerializers(Out);
    }

    [Fact]
    public void TypeDecoratingUniSerializedSerialization()
    {
        TypeDecoratingUniSerialized.New(default(Unit)).AssertPassesThroughAllSerializers(Out);
        TypeDecoratingUniSerialized.New(1).AssertPassesThroughAllSerializers(Out);
        TypeDecoratingUniSerialized.New((int?)null).AssertPassesThroughAllSerializers(Out);
        TypeDecoratingUniSerialized.New((int?)1).AssertPassesThroughAllSerializers(Out);
        TypeDecoratingUniSerialized.New((string?)null).AssertPassesThroughAllSerializers(Out);
        TypeDecoratingUniSerialized.New("").AssertPassesThroughAllSerializers(Out);
        TypeDecoratingUniSerialized.New("A").AssertPassesThroughAllSerializers(Out);
    }

    [Fact]
    public void PropertyBagItemSerialization()
    {
        PropertyBagItem.New("X", default(Unit)).AssertPassesThroughAllSerializers(Out);
        var a = PropertyBagItem.New("X", 3).AssertPassesThroughAllSerializers(Out);
        a.Value.Should().Be(3);
        var b = PropertyBagItem.New("X", (int?)3).AssertPassesThroughAllSerializers(Out);
        b.Value.Should().Be(3);
    }

    [Fact]
    public void PropertyBagSerialization()
    {
        default(PropertyBag).AssertPassesThroughAllSerializers();
        var s = new PropertyBag();
        s.KeylessSet(default(Unit));
        s.KeylessSet(3);
        s.KeylessSet((int?)4);
        s.KeylessSet("X");
        WriteLine(s.ToString());
        var s1 = s.PassThroughSystemJsonSerializer(Out);
        WriteLine(s1.ToString());
        s1.Items.Should().BeEquivalentTo(s.Items, o => o.ComparingRecordsByMembers());
    }

    [Fact]
    public void MutablePropertyBagSerialization()
    {
        default(MutablePropertyBag).AssertPassesThroughAllSerializers();
        var s = new MutablePropertyBag();
        s.KeylessSet(default(Unit));
        s.KeylessSet(3);
        s.KeylessSet((int?)4);
        s.KeylessSet("X");
        WriteLine(s.ToString());
        var s1 = s.PassThroughAllSerializers(Out);
        WriteLine(s.ToString());
        s1.Items.Should().BeEquivalentTo(s.Items, o => o.ComparingRecordsByMembers());
    }

    [Fact]
    public void ValueTupleSerialization()
    {
        // System.Text.Json fails to serialize ValueTuple fields, see:
        // - https://stackoverflow.com/questions/70436689/net-jsonserializer-does-not-serialize-tuples-values
        var s = (1, "X");
        WriteLine(s.ToString());
        var s1 = s.PassThroughSystemJsonSerializer(Out);
        WriteLine(s1.ToString());
        s1.Should().NotBe(s);
    }

    [Fact]
    public void BoxSerialization()
    {
        Test(0);
        Test(1);
        Test((string?)null);
        Test("");
        Test("s");
        return;

        void Test<T>(T value) {
            var box = Box.New(value);
            var serializedBox = box.PassThroughAllSerializers();
            serializedBox.Should().Be(box);
            serializedBox.Value.Should().Be(value);
        }
    }

    [Fact]
    public void MutableBoxSerialization()
    {
        Test(0);
        Test(1);
        Test((string?)null);
        Test("");
        Test("s");
        return;

        void Test<T>(T value) {
            var box = MutableBox.New(value);
            var serializedBox = box.PassThroughAllSerializers();
            serializedBox.Value.Should().Be(value);
        }
    }
}

using ActualLab.Collections.Internal;
using ActualLab.Interception;
using ActualLab.IO;
using ActualLab.Reflection;
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
        Out.WriteLine(json);

        var deserialized = (Tuple<DateTime>)serializer.Read<object>(json);
        deserialized.Item1.Should().Be(value.Item1);
    }

    [Fact]
    public void UnitSerialization()
    {
        default(Unit).AssertPassesThroughAllSerializers(Out);
        var list = ArgumentList.New(default(Unit));
        list.AssertPassesThroughAllSerializers(Out);
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
        var r = Result.Error<int>(new InvalidOperationException()).PassThroughAllSerializers();
        r.Error.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void ResultBoxSerialization()
    {
        default(ResultBox<int>).AssertPassesThroughAllSerializers(Out);
        var r = new ResultBox<int>(1).PassThroughAllSerializers(Out);
        r.Value.Should().Be(1);
        r = new ResultBox<int>(0, new InvalidOperationException()).PassThroughAllSerializers();
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
            Out.WriteLine(s?.ToString() ?? "null");
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
        Test(new RpcHandshake(default, null, default));
        Test(new RpcHandshake(new Guid(), new VersionSet(("Test", "1.0")), new Guid()));

        void Test(RpcHandshake h) {
            var hs = h.PassThroughAllSerializers();
            hs.RemotePeerId.Should().Be(h.RemotePeerId);
        }
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
    public void RpcMessageSerialization()
    {
        Test(new RpcMessage(0, 3, "s", "m",
            new TextOrBytes([1, 2, 3]),
            null));

        Test(new RpcMessage(1, 3, "s", "m",
            new TextOrBytes([1, 2, 3]),
            []));

        Test(new RpcMessage(2, 3, "s", "m",
            new TextOrBytes([1, 2, 3]),
            [
                new("v", "@OVhtp0TRc")
            ]));

        Test(new RpcMessage(0, 3, "s", "m",
            new TextOrBytes([1, 2, 3]),
            [
                new("a", "b"),
                new("v", "@OVhtp0TRc")
            ]));

        void Test(RpcMessage m) {
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
    public void Base64DataSerialization()
    {
        Test(default);
        Test(new Base64Encoded(null!));
        Test(new Base64Encoded([]));
        Test(new Base64Encoded([1]));
        Test(new Base64Encoded([1, 2]));

        void Test(Base64Encoded src) {
            var dst = src.PassThroughAllSerializers(Out);
            src.Data.SequenceEqual(dst.Data).Should().BeTrue();
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
        Test(new TextOrBytes(Array.Empty<byte>()));
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
        default(ImmutableOptionSet).AssertPassesThroughAllSerializers(Out);
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
        s.Set(default(Unit));
        s.Set(3);
        s.Set((int?)4);
        s.Set("X");
        Out.WriteLine(s.ToString());
        var s1 = s.PassThroughSystemJsonSerializer(Out);
        Out.WriteLine(s1.ToString());
        s1.Items.Should().BeEquivalentTo(s.Items, o => o.ComparingRecordsByMembers());
    }

    [Fact]
    public void MutablePropertyBagSerialization()
    {
        default(MutablePropertyBag).AssertPassesThroughAllSerializers();
        var s = new MutablePropertyBag();
        s.Set(default(Unit));
        s.Set(3);
        s.Set((int?)4);
        s.Set("X");
        Out.WriteLine(s.ToString());
        var s1 = s.PassThroughAllSerializers(Out);
        Out.WriteLine(s.ToString());
        s1.Items.Should().BeEquivalentTo(s.Items, o => o.ComparingRecordsByMembers());
    }

    [Fact]
    public void ValueTupleSerialization()
    {
        // System.Text.Json fails to serialize ValueTuple fields, see:
        // - https://stackoverflow.com/questions/70436689/net-jsonserializer-does-not-serialize-tuples-values
        var s = (1, "X");
        Out.WriteLine(s.ToString());
        var s1 = s.PassThroughSystemJsonSerializer(Out);
        Out.WriteLine(s1.ToString());
        s1.Should().NotBe(s);
    }
}

using ActualLab.Compliance;
using ActualLab.IO;
using ActualLab.Reflection;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Serialization.Internal;

namespace ActualLab.Tests.Serialization;

public class MessagePackNilTest(ITestOutputHelper @out) : TestBase(@out)
{
    private static readonly ReadOnlyMemory<byte> Nil = new([0xC0]);

    [Fact]
    public void NilReadsAsDefaultTest()
    {
        // Nil is what an array-form MessagePackObject slot holds when the blob predates the property,
        // so every hand-written formatter must read it as default instead of throwing.
        AssertNilReadsAsDefault<Unit>();
        AssertNilReadsAsDefault<Moment>();
        AssertNilReadsAsDefault<CpuTimestamp>();
        AssertNilReadsAsDefault<Option<int>>();
        AssertNilReadsAsDefault<ApiOption<int>>();
        AssertNilReadsAsDefault<ApiNullable<int>>();
        AssertNilReadsAsDefault<ApiNullable8<int>>();
        AssertNilReadsAsDefault<ApiArray<int>>();
        AssertNilReadsAsDefault<Result<int>>();
        AssertNilReadsAsDefault<PropertyBag>();
        // default(PropertyBagItem).Equals dereferences its null Key, so this one is checked by hand
        ReadNil<PropertyBagItem>(MessagePackByteSerializer.Default).Key.Should().BeNull();
        ReadNil<PropertyBagItem>(NerdbankMessagePackByteSerializer.Default).Key.Should().BeNull();
        AssertNilReadsAsDefault<TypeDecoratingUniSerialized<TypeSchema.Any, object>>();
        AssertNilReadsAsDefault<Symbol>();
        AssertNilReadsAsDefault<FilePath>();
        AssertNilReadsAsDefault<TypeRef>();
        AssertNilReadsAsDefault<SanitizedString<Sanitizers.Hidden>>();
        AssertNilReadsAsDefault<ByteString>();
        AssertNilReadsAsDefault<MessagePackData>();
    }

    [Fact]
    public void NilReadsAsDefaultNerdbankOnlyTest()
    {
        // MessagePack-CSharp reads these structs via source-generated formatters, which throw on nil
        var serializer = NerdbankMessagePackByteSerializer.Default;
        AssertNilReadsAsDefault<RpcObjectId>(serializer);
        AssertNilReadsAsDefault<RpcHeader>(serializer);
        AssertNilReadsAsDefault<RpcHeaderKey>(serializer);
        AssertNilReadsAsDefault<RpcMethodRef>(serializer);
    }

    // Private methods

    private static void AssertNilReadsAsDefault<T>()
    {
        AssertNilReadsAsDefault<T>(MessagePackByteSerializer.Default);
        AssertNilReadsAsDefault<T>(NerdbankMessagePackByteSerializer.Default);
    }

    private static void AssertNilReadsAsDefault<T>(IByteSerializer serializer)
        => ReadNil<T>(serializer).Should()
            .Be(default(T), "{0} must read nil as default", serializer.GetType().GetName());

    private static T ReadNil<T>(IByteSerializer serializer)
        => (T)serializer.Read(Nil, typeof(T), out _)!;
}

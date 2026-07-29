using ActualLab.Compliance;
using ActualLab.Serialization;
using MemoryPack;
using MessagePack;

namespace ActualLab.Tests.Compliance;

/// <summary>
/// Pins what it takes to hold a <see cref="SanitizedString{TSanitizer}"/> as a member of a
/// serializable type. The wire stays identical to a plain string, but MemoryPack needs the
/// member annotated - see the comment on <see cref="WithSanitized"/>.
/// </summary>
public partial class SanitizedStringMemberTest
{
    private const string Secret = "hunter2-hunter2";

    [Fact]
    public void MemberIsWireCompatibleWithAStringMember()
    {
        var sanitized = new WithSanitized(Secret);
        var plain = new WithPlainString(Secret);

        foreach (var serializer in ByteSerializers()) {
            using var a = serializer.Write(sanitized, typeof(WithSanitized));
            using var b = serializer.Write(plain, typeof(WithPlainString));
            a.WrittenMemory.ToArray().Should().Equal(b.WrittenMemory.ToArray());
        }

        foreach (var serializer in TextSerializers())
            serializer.Write(sanitized).Should().Be(serializer.Write(plain));
    }

    [Fact]
    public void MemberSerializesRawWhileSanitizationIsActive()
    {
        var value = new WithSanitized(Secret);
        Sanitization.IsSuspended.Should().BeFalse();

        // Masking is a rendering concern: ToString() hides it, serialization must not
        value.ToString().Should().NotContain(Secret);
        foreach (var serializer in TextSerializers())
            serializer.Write(value).Should().Contain(Secret);
    }

    // Nested types

    [MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
    public partial record WithSanitized(
        // [MemoryPackAllowSerialize] is required: SanitizedString<T>'s formatter is registered at
        // runtime, and without it the containing type fails to compile with MEMPACK019
        [property: MemoryPackOrder(0), MemoryPackAllowSerialize, Key(0)]
        SanitizedString<Sanitizers.Hidden> Secret);

    [MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
    public partial record WithPlainString(
        [property: MemoryPackOrder(0), Key(0)] string Secret);

    // Private methods

    private static IByteSerializer[] ByteSerializers()
        => [MemoryPackByteSerializer.Default, MessagePackByteSerializer.Default];

    private static ITextSerializer[] TextSerializers()
        => [SystemJsonSerializer.Default, NewtonsoftJsonSerializer.Default];
}

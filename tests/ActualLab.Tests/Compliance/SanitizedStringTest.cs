using ActualLab.Compliance;
using ActualLab.Serialization;

namespace ActualLab.Tests.Compliance;

public class SanitizedStringTest
{
    private const string Secret = "hunter2-hunter2";

    [Fact]
    public void RendersMaskedWhileActive()
    {
        var value = new SanitizedString<Sanitizer.PrefixAndLengthHint>(Secret);

        using (Sanitization.Begin())
            value.ToString().Should().Be("<<hu* [8-15]>>");

        using (Sanitization.Suspend())
            value.ToString().Should().Be(Secret);
    }

    [Fact]
    public void SuspendOverridesIsAlwaysActive()
    {
        // This is the whole reason IsActive is tri-state rather than an OR: with
        // IsAlwaysActive at its default, a test still has to be able to read the raw value.
        Sanitization.IsAlwaysActive.Should().BeTrue();
        Sanitization.IsActive.Should().BeTrue();

        using (Sanitization.Suspend()) {
            Sanitization.IsActive.Should().BeFalse();
            using (Sanitization.Begin())
                Sanitization.IsActive.Should().BeTrue();
            Sanitization.IsActive.Should().BeFalse();
        }

        Sanitization.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("abc")]
    [InlineData(Secret)]
    public void IsWireCompatibleWithString(string source)
    {
        // The point of the type: changing a member from string to SanitizedString<T> must not
        // be a wire change. Every format must produce the bytes a plain string produces.
        var value = new SanitizedString<Sanitizer.LengthHint>(source);

        var type = typeof(SanitizedString<Sanitizer.LengthHint>);
        foreach (var serializer in ByteSerializers()) {
            using var expectedBuffer = serializer.Write(source, typeof(string));
            using var actualBuffer = serializer.Write(value, type);
            var expected = expectedBuffer.WrittenMemory.ToArray();
            var actual = actualBuffer.WrittenMemory.ToArray();
            actual.Should().Equal(expected, "byte format must match string");

            // Both directions: a string payload reads back as SanitizedString<T>, and vice versa
            ((SanitizedString<Sanitizer.LengthHint>)serializer.Read(expected, type, out _)!).Value
                .Should().Be(source);
            ((string)serializer.Read(actual, typeof(string), out _)!).Should().Be(source);
        }

        foreach (var serializer in TextSerializers()) {
            var expected = serializer.Write(source);
            var actual = serializer.Write(value);
            actual.Should().Be(expected, "text format must match string");

            serializer.Read<SanitizedString<Sanitizer.LengthHint>>(expected).Value.Should().Be(source);
            serializer.Read<string>(actual).Should().Be(source);
        }
    }

    [Fact]
    public void SerializationCarriesTheRawValueEvenWhileActive()
    {
        // Masking is a rendering concern - a masked value on the wire would be silent data loss.
        var value = new SanitizedString<Sanitizer.LengthHint>(Secret);
        using var _ = Sanitization.Begin();

        foreach (var serializer in TextSerializers())
            serializer.Write(value).Should().Contain(Secret);
    }

    [Fact]
    public void EqualityIsOnTheRawValue()
    {
        var a = new SanitizedString<Sanitizer.LengthHint>(Secret);
        var b = new SanitizedString<Sanitizer.LengthHint>(Secret);
        var c = new SanitizedString<Sanitizer.LengthHint>("abcdefghijklmno"); // same length as Secret

        using var _ = Sanitization.Begin();
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
        a.Should().NotBe(c);
        // ...even though both mask to the same text, being the same length
        a.ToString().Should().Be(c.ToString());
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("a", "<<* [1]>>")]
    [InlineData("ab", "<<* [2-3]>>")]
    [InlineData("abc", "<<* [2-3]>>")]
    [InlineData("abcd", "<<ab* [4-7]>>")]
    [InlineData("abcdefgh", "<<ab* [8-15]>>")]
    public void PrefixAndLengthHintFallsBackForShortValues(string source, string expected)
    {
        using var _ = Sanitization.Begin();
        new SanitizedString<Sanitizer.PrefixAndLengthHint>(source).ToString().Should().Be(expected);
    }

    [Fact]
    public void HiddenAndFingerprintBehaveAsAdvertised()
    {
        using var _ = Sanitization.Begin();
        new SanitizedString<Sanitizer.Hidden>(Secret).ToString().Should().Be(Sanitizer.HiddenValue);

        var fingerprint = new SanitizedString<Sanitizer.Fingerprint>(Secret).ToString();
        fingerprint.Should().MatchRegex("^<<[0-9a-f]{8}>>$");
        fingerprint.Should().NotContain(Secret);
        // Same value -> same fingerprint, which is what makes it usable for correlation
        new SanitizedString<Sanitizer.Fingerprint>(Secret).ToString().Should().Be(fingerprint);
    }

    // Private methods

    private static IByteSerializer[] ByteSerializers()
        => [MemoryPackByteSerializer.Default, MessagePackByteSerializer.Default];

    private static ITextSerializer[] TextSerializers()
        => [SystemJsonSerializer.Default, NewtonsoftJsonSerializer.Default];
}

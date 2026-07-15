using System.Text;
using ActualLab.Diagnostics;
using ActualLab.IO;
using ActualLab.Reflection;

namespace ActualLab.Tests.CoreAudit;

[CollectionDefinition(nameof(CoreRemainingAuditCollection), DisableParallelization = true)]
public class CoreRemainingAuditCollection;

[Collection(nameof(CoreRemainingAuditCollection))]
public class CoreRemainingAuditRegressionTest
{
    private sealed class PropertySource(int readOnlyValue)
    {
        public int Writable { get; set; }
        public int ReadOnly { get; } = readOnlyValue;
    }

    private sealed class IndexedSource
    {
        public int Writable { get; set; }
        public int this[int index] {
            get => index;
            set { }
        }
    }

    private sealed class UntypedValueSource
    {
        public int Value { get; set; }
    }

    [Fact]
    public void Base64UrlEncodingShouldFollowRfc4648Alphabet()
    {
        var data = new byte[] { 0xFB, 0xFF };

        Base64UrlEncoder.Encode(data).Should().Be("-_8");
        Base64UrlEncoder.Decode("-_8").ToArray().Should().Equal(data);
    }

    [Fact]
    public void Base64UrlDecoderShouldRejectNonAsciiInput()
    {
        Func<byte[]> action = () => Base64UrlEncoder.Decode("ŁAAA").ToArray();

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Base64UrlDecoderShouldRejectStandardBase64Alphabet()
    {
        Func<byte[]> action = () => Base64UrlEncoder.Decode("+/8").ToArray();

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EmptyTypeRefShouldHaveAnEmptyTypeName()
        => TypeRef.None.TypeName.Should().BeEmpty();

    [Fact]
    public void UnqualifiedTypeRefShouldKeepItsWholeTypeName()
        => new TypeRef("Namespace.Type").TypeName.Should().Be("Namespace.Type");

    [Fact]
    public void MemberwiseCopierShouldSkipReadOnlyProperties()
    {
        var source = new PropertySource(1) { Writable = 3 };
        var target = new PropertySource(2);

        MemberwiseCopier.Invoke(source, target);

        target.Writable.Should().Be(3);
        target.ReadOnly.Should().Be(2);
    }

    [Fact]
    public void MemberwiseCopierShouldSkipIndexedProperties()
    {
        var source = new IndexedSource { Writable = 3 };
        var target = new IndexedSource();

        MemberwiseCopier.Invoke(source, target);

        target.Writable.Should().Be(3);
    }

    [Fact]
    public void DynamicMethodSetterShouldUnboxUntypedValues()
    {
        var oldMode = RuntimeCodegen.Mode;
        try {
            RuntimeCodegen.Mode = RuntimeCodegenMode.DynamicMethods;
            var property = typeof(UntypedValueSource).GetProperty(nameof(UntypedValueSource.Value))!;
            var setter = property.GetSetter<object, object>(true);
            var target = new UntypedValueSource();

            setter(target, 3);

            target.Value.Should().Be(3);
        }
        finally {
            RuntimeCodegen.Mode = oldMode;
        }
    }

    [Fact]
    public void ConcurrentSamplerShouldSupportOneShard()
        => Sampler.Always.ToConcurrent(1).Next().Should().BeTrue();

    [Fact]
    public void ApplicationTempDirectoryShouldExposeAlwaysCreateApi()
    {
        var method = typeof(FilePath).GetMethods()
            .Single(x => x.Name == nameof(FilePath.GetApplicationTempDirectory));

        method.GetParameters().Select(x => x.ParameterType).Should().Equal(typeof(string));
    }

    [Fact]
    public void NewtonsoftByteReaderShouldReportOnlyConsumedValueLength()
    {
        var data = Encoding.UTF8.GetBytes("1\n2").AsMemory();

        NewtonsoftJsonSerializer.Default.Read(data, typeof(int), out var readLength).Should().Be(1);
        data = data[readLength..];
        NewtonsoftJsonSerializer.Default.Read(data, typeof(int), out _).Should().Be(2);
    }

    [Fact]
    public void NewtonsoftByteReaderShouldTrackUtf8AndLineBreaks()
    {
        const string json = "{\"text\":\"é\"}";
        var data = Encoding.UTF8.GetBytes(json + "\r\n2").AsMemory();

        NewtonsoftJsonSerializer.Default.Read(data, typeof(Dictionary<string, string>), out var readLength);

        readLength.Should().Be(Encoding.UTF8.GetByteCount(json));
        NewtonsoftJsonSerializer.Default.Read(data[readLength..], typeof(int), out _).Should().Be(2);
    }

    [Fact]
    public void NewtonsoftByteReaderShouldTrackValuesLargerThanItsBuffer()
    {
        var json = $"\"{new string('a', 5_000)}\"";
        var data = Encoding.UTF8.GetBytes(json + "\n2").AsMemory();

        NewtonsoftJsonSerializer.Default.Read(data, typeof(string), out var readLength);

        readLength.Should().Be(Encoding.UTF8.GetByteCount(json));
        NewtonsoftJsonSerializer.Default.Read(data[readLength..], typeof(int), out _).Should().Be(2);
    }

#if !NET7_0_OR_GREATER
    [Fact]
    public async Task LegacyStreamReaderOverloadsShouldHonorPreCancellation()
    {
        using var lineStream = new MemoryStream(Encoding.UTF8.GetBytes("text"));
        using var lineReader = new StreamReader(lineStream);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("text"));
        using var reader = new StreamReader(stream);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await lineReader.Invoking(x => x.ReadLineAsync(cancellationSource.Token))
            .Should().ThrowAsync<OperationCanceledException>();
        await reader.Invoking(x => x.ReadToEndAsync(cancellationSource.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }
#endif
}

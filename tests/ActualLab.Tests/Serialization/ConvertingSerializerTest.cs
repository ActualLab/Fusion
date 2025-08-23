using ActualLab.Conversion;

namespace ActualLab.Tests.Serialization;

public class ConvertingSerializerTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void ByteSerializerConverterTest()
    {
        var serializer = ByteSerializer.Default.ToTyped<string>().Convert(BiConverter.Identity<string>());
        var value = "test";
        var data = serializer.Write(value).WrittenMemory;
        serializer.Read(data, out _).Should().Be(value);
    }

    [Fact]
    public void TextSerializerConverterTest()
    {
        var serializer = TextSerializer.Default.ToTyped<string>().Convert(BiConverter.Identity<string>());
        var value = "test";
        var data = serializer.Write(value);
        serializer.Read(data).Should().Be(value);
    }
}

using System.Text.Json;
using ActualLab.IO;
using ActualLab.Reflection;

namespace ActualLab.Tests.Serialization;

/// <summary>
/// System.Text.Json routes non-string dictionary keys through the key converter's
/// ReadAsPropertyName/WriteAsPropertyName, and JsonConverter&lt;T&gt;'s defaults throw
/// NotSupportedException - so a value type that round-trips fine as a value can still be
/// unusable as a dictionary key. These are the ActualLab key types that must work.
/// </summary>
public class JsonDictionaryKeyTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void SymbolKeysRoundTrip()
        => AssertRoundTrip(new Dictionary<Symbol, int> {
            [new Symbol("a")] = 1,
            [new Symbol("b.c")] = 2,
        });

    [Fact]
    public void TypeRefKeysRoundTrip()
        => AssertRoundTrip(new Dictionary<TypeRef, int> {
            [new TypeRef(typeof(string))] = 1,
            [new TypeRef(typeof(List<int>))] = 2,
        });

    [Fact]
    public void MomentKeysRoundTrip()
        => AssertRoundTrip(new Dictionary<Moment, int> {
            [new Moment(DateTime.UnixEpoch)] = 1,
            [new Moment(DateTime.UnixEpoch.AddDays(1))] = 2,
        });

    [Fact]
    public void ByteStringKeysRoundTrip()
        => AssertRoundTrip(new Dictionary<ByteString, int> {
            [ByteString.FromStringAsUtf8("a")] = 1,
            [ByteString.FromStringAsUtf8("bc")] = 2,
        });

    [Fact]
    public void FilePathKeysRoundTrip()
        => AssertRoundTrip(new Dictionary<FilePath, int> {
            [new FilePath("a.txt")] = 1,
            [new FilePath("b/c.txt")] = 2,
        });

    // Private methods

    private void AssertRoundTrip<TKey>(Dictionary<TKey, int> source)
        where TKey : notnull
    {
        var json = JsonSerializer.Serialize(source);
        @out.WriteLine(json);
        JsonSerializer.Deserialize<Dictionary<TKey, int>>(json).Should().Equal(source);
    }
}

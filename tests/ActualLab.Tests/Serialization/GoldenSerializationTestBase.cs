#if NET8_0_OR_GREATER
using System.Buffers;
using System.Text;
using ActualLab.IO;

namespace ActualLab.Tests.Serialization;

/// <summary>
/// Compares the output of every serializer for a set of named cases with a checked-in golden file,
/// which is (re)written instead when <see cref="RecordEnvVar"/> is set to <c>1</c>.
/// </summary>
public abstract class GoldenSerializationTestBase(ITestOutputHelper @out) : TestBase(@out)
{
    public const string RecordEnvVar = "ACTUALLAB_RECORD_GOLDEN";
    public const string RoundTripSuffix = ":RoundTrip";

    protected static readonly GoldenSerializer[] Serializers = [
        GoldenSerializer.New("MemoryPack", MemoryPackByteSerializer.Default),
        GoldenSerializer.New("MemoryPack.TypeDecorating", MemoryPackByteSerializer.DefaultTypeDecorating),
        GoldenSerializer.New("MessagePack", MessagePackByteSerializer.Default),
        GoldenSerializer.New("MessagePack.TypeDecorating", MessagePackByteSerializer.DefaultTypeDecorating),
        GoldenSerializer.New("Nerdbank", NerdbankMessagePackByteSerializer.Default),
        GoldenSerializer.New("SystemJson", SystemJsonSerializer.Default),
        GoldenSerializer.New("SystemJson.TypeDecorating", SystemJsonSerializer.DefaultTypeDecorating),
        GoldenSerializer.New("NewtonsoftJson", NewtonsoftJsonSerializer.Default),
        GoldenSerializer.New("NewtonsoftJson.TypeDecorating", NewtonsoftJsonSerializer.DefaultTypeDecorating),
    ];

    protected abstract IEnumerable<GoldenCase> GetCases();

    // Serializers whose name starts with one of the skipped prefixes get a "!skipped" line
    protected static GoldenCase Case<T>(string name, T value, params string[] skippedSerializerPrefixes)
        => new(name, typeof(T), value) { SkippedSerializerPrefixes = skippedSerializerPrefixes };

    protected void AssertOutputMatchesGolden([CallerFilePath] string sourceFilePath = "")
    {
        // arrange
        var sourcePath = FilePath.New(sourceFilePath);
        var goldenPath = sourcePath.DirectoryPath & "Golden" & (sourcePath.FileNameWithoutExtension + ".txt");
        var cases = GetCases().ToList();

        // act
        var actual = ComputeLines(cases);

        // assert
        if (Environment.GetEnvironmentVariable(RecordEnvVar) == "1") {
            WriteGoldenFile(goldenPath, actual);
            Out.WriteLine($"Recorded {actual.Count} lines to {goldenPath}");
            return;
        }

        File.Exists(goldenPath).Should()
            .BeTrue($"golden file {goldenPath} must exist; record it with {RecordEnvVar}=1");
        var expected = ReadGoldenFile(goldenPath);
        var mismatches = new List<string>();
        foreach (var (key, actualOutput) in actual) {
            if (!expected.TryGetValue(key, out var expectedOutput))
                mismatches.Add($"+ {key} | {actualOutput}");
            else if (!string.Equals(expectedOutput, actualOutput, StringComparison.Ordinal))
                mismatches.Add($"! {key}\n    expected: {expectedOutput}\n    actual:   {actualOutput}");
        }
        foreach (var (key, expectedOutput) in expected)
            if (!actual.ContainsKey(key))
                mismatches.Add($"- {key} | {expectedOutput}");
        foreach (var mismatch in mismatches)
            Out.WriteLine(mismatch);
        mismatches.Should().BeEmpty(
            $"serializer output must match {goldenPath} ({mismatches.Count} mismatching line(s), see the test output)");
    }

    // Private methods

    private static Dictionary<string, string> ComputeLines(List<GoldenCase> cases)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in cases)
        foreach (var serializer in Serializers) {
            var key = $"{c.Name} | {serializer.Name}";
            if (result.ContainsKey(key))
                throw new InvalidOperationException($"Duplicate golden case: {c.Name}");

            if (c.SkippedSerializerPrefixes.Any(p => serializer.Name.StartsWith(p, StringComparison.Ordinal))) {
                result.Add(key, "!skipped");
                continue;
            }

            string output;
            try {
                output = serializer.Write(c.Value, c.Type);
            }
            catch (Exception e) {
                result.Add(key, FormatError(e));
                continue;
            }

            result.Add(key, Escape(output));
            result.Add(key + RoundTripSuffix, GetRoundTripResult(serializer, c, output));
        }
        return result;
    }

    private static string GetRoundTripResult(GoldenSerializer serializer, GoldenCase c, string output)
    {
        try {
            var value = serializer.Read(output, c.Type);
            if (Equals(value, c.Value))
                return "ok";

            var output2 = serializer.Write(value, c.Type);
            return string.Equals(output, output2, StringComparison.Ordinal)
                ? "ok"
                : "mismatch: " + Escape(output2);
        }
        catch (Exception e) {
            return FormatError(e);
        }
    }

    private static void WriteGoldenFile(FilePath path, Dictionary<string, string> lines)
    {
        Directory.CreateDirectory(path.DirectoryPath);
        var sb = new StringBuilder();
        sb.Append("# Format: case | serializer | output (hex for binary serializers, text for JSON ones).\n");
        sb.Append($"# Re-record with {RecordEnvVar}=1; review the diff before committing it.\n");
        foreach (var (key, output) in lines)
            sb.Append(key).Append(" | ").Append(output).Append('\n');
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static Dictionary<string, string> ReadGoldenFile(FilePath path)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rawLine in File.ReadAllText(path).Split('\n')) {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var firstSeparator = line.IndexOf(" | ", StringComparison.Ordinal);
            var secondSeparator = line.IndexOf(" | ", firstSeparator + 3, StringComparison.Ordinal);
            result.Add(line[..secondSeparator], line[(secondSeparator + 3)..]);
        }
        return result;
    }

    private static string FormatError(Exception e)
        => "!error: " + e.GetType().Name;

    private static string Escape(string output)
        => output.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
}

public sealed record GoldenCase(string Name, Type Type, object? Value)
{
    public string[] SkippedSerializerPrefixes { get; init; } = [];
}

public sealed record GoldenSerializer(
    string Name,
    Func<object?, Type, string> Write,
    Func<string, Type, object?> Read)
{
    public static GoldenSerializer New(string name, IByteSerializer serializer)
        => serializer is ITextSerializer textSerializer
            ? new(name, textSerializer.Write, textSerializer.Read)
            : new(name,
                (value, type) => {
                    var buffer = new ArrayBufferWriter<byte>();
                    serializer.Write(buffer, value, type);
                    return Convert.ToHexString(buffer.WrittenSpan).ToLowerInvariant();
                },
                (data, type) => serializer.Read(Convert.FromHexString(data), type, out _));
}
#endif

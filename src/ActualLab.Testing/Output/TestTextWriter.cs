using System.Text;
using Xunit.Abstractions;

namespace ActualLab.Testing.Output;

public class TestTextWriter(ITestOutputHelper? downstream = null) : TextWriter, ITestOutputHelper
{
    protected static readonly string EnvNewLine = Environment.NewLine;
    protected static readonly char LastEnvNewLineChar = EnvNewLine[^1];
    protected static readonly string LastEnvNewLineString = LastEnvNewLineChar.ToString();

    protected StringBuilder Prefix = new();
    public override Encoding Encoding { get; } = Encoding.UTF8;
    public ITestOutputHelper? Downstream { get; } = downstream;

    public override void Write(char value)
    {
        if (value == LastEnvNewLineChar)
            Write(value.ToString());
        else
            Prefix.Append(value);
    }

    public override void Write(string? value)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value));

        Prefix.Append(value);
#if NETCOREAPP3_1_OR_GREATER
        if (!value.Contains(LastEnvNewLineChar, StringComparison.Ordinal))
#else
        if (!value.Contains(LastEnvNewLineChar))
#endif
            return;

        var lines = Prefix.ToString().Split(EnvNewLine);
        // lines.Length >= 1 here for sure
        if (Downstream is not null)
            foreach (var line in lines.Take(lines.Length))
                Downstream.WriteLine(line);
        Prefix.Clear();
        Prefix.Append(lines[^1]);
    }
}

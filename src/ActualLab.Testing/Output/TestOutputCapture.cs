using System.CommandLine.IO;
using System.Text;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Xunit.Abstractions;

namespace ActualLab.Testing.Output;

public class TestOutputCapture(TestTextWriter? downstream = null)
    : IStandardStreamWriter, ITestOutputHelper
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    public StringBuilder StringBuilder = new();
    public TestTextWriter? Downstream { get; set; } = downstream;

    public TestOutputCapture(ITestOutputHelper downstream)
        : this(new TestTextWriter(downstream))
    { }

    public override string ToString()
    {
        lock (_lock) {
            return StringBuilder.ToString();
        }
    }

    public void WriteLine(string message)
        => Write(message + Environment.NewLine);

    public void WriteLine(string format, params object[] args)
#pragma warning disable MA0011
#pragma warning disable CA1305 // Could vary based on the current user's locale settings
        => WriteLine(string.Format(format, args));
#pragma warning restore CA1305
#pragma warning restore MA0011

    public void Write(char value)
    {
        lock (_lock) {
            StringBuilder.Append(value);
            Downstream?.Write(value);
        }
    }

    public void Write(string? value)
    {
        lock (_lock) {
            StringBuilder.Append(value);
            Downstream?.Write(value);
        }
    }

    public TestOutputCapture Clear()
    {
        lock (_lock) {
            StringBuilder.Clear();
            return this;
        }
    }

    public StringAssertions Should()
        => new(ToString(), AssertionChain.GetOrCreate());
}

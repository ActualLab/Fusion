using Xunit.Abstractions;

namespace ActualLab.Testing;

/// <summary>
/// An <see cref="ITestOutputHelper"/> implementation that writes to the console,
/// useful when running tests outside of xUnit's captured output context.
/// </summary>
public class ConsoleTestOutputHelper : ITestOutputHelper
{
    public void WriteLine(string message)
        => Console.WriteLine(message);

    public void WriteLine(string format, params object[] args)
        => Console.WriteLine(format, args);
}

using System.Text;

namespace ActualLab.Testing.Logging;

public sealed class CapturingLoggerProvider(Func<CapturingLoggerProvider, string, ILogger> loggerFactory)
    : ILoggerProvider
{
    private readonly StringBuilder _buffer = new();

    public CpuTimestamp StartedAt { get; } = CpuTimestamp.Now;
    public string Content => UseBuffer(buffer => buffer.ToString());

    public CapturingLoggerProvider()
        : this(DefaultLoggerFactory)
    { }

    public void Dispose() { }

    public void UseBuffer(Action<StringBuilder> accessor)
    {
        lock (_buffer)
            accessor.Invoke(_buffer);
    }

    public T UseBuffer<T>(Func<StringBuilder, T> accessor)
    {
        lock (_buffer)
            return accessor.Invoke(_buffer);
    }

    public void Clear()
        => UseBuffer(b => b.Clear());

    public ILogger CreateLogger(string categoryName)
        => loggerFactory.Invoke(this, categoryName);

    // Private methods

    private static ILogger DefaultLoggerFactory(CapturingLoggerProvider provider, string category)
        => new CapturingLogger(provider, category);
}

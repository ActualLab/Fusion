using System.Globalization;
using System.Text;
using Cysharp.Text;

namespace ActualLab.IO.Internal;

public sealed class Utf8TextWriter(IFormatProvider formatProvider) : TextWriter(formatProvider)
{
    private Utf8ValueStringBuilder _sb = ZString.CreateUtf8StringBuilder();

    public ref Utf8ValueStringBuilder Buffer => ref _sb;
    public override Encoding Encoding => EncodingExt.Utf8NoBom;

    public Utf8TextWriter()
        : this(CultureInfo.CurrentCulture)
    { }

    protected override void Dispose(bool disposing)
    {
        _sb.Dispose();
        base.Dispose(disposing);
    }

    public override string ToString() => _sb.ToString();

    public override void Close()
        => Dispose(true);

    public void WriteLiteral(byte value)
    {
        var span = _sb.GetSpan(1);
        span[0] = value;
        _sb.Advance(1);
    }

    public void WriteLiteral(ReadOnlySpan<byte> bytes)
        => _sb.AppendLiteral(bytes);

    public override void Write(char value)
        => _sb.Append(value);

    public override void Write(char[] buffer, int index, int count)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (buffer.Length - index < count)
            throw new ArgumentException();

        _sb.Append(buffer.AsSpan(index, count));
    }

    public override void Write(string? value)
    {
        if (value == null)
            return;

        _sb.Append(value);
    }

    public override Task WriteAsync(char value)
    {
        Write(value);
        return Task.CompletedTask;
    }

    public override Task WriteAsync(string? value)
    {
        Write(value);
        return Task.CompletedTask;
    }

    public override Task WriteAsync(char[] buffer, int index, int count)
    {
        Write(buffer, index, count);
        return Task.CompletedTask;
    }

    public override Task WriteLineAsync(char value)
    {
        WriteLine(value);
        return Task.CompletedTask;
    }

    public override Task WriteLineAsync(string? value)
    {
        WriteLine(value);
        return Task.CompletedTask;
    }

    public override Task WriteLineAsync(char[] buffer, int index, int count)
    {
        WriteLine(buffer, index, count);
        return Task.CompletedTask;
    }

    public override void Write(bool value)
        => _sb.Append<bool>(value);

    public override void Write(decimal value)
        => _sb.Append(value);

#if !NETSTANDARD2_0
    public override void Write(ReadOnlySpan<char> buffer)
        => _sb.Append(buffer);

    public override void WriteLine(ReadOnlySpan<char> buffer)
    {
        _sb.Append(buffer);
        WriteLine();
    }

    public override Task WriteAsync(
        ReadOnlyMemory<char> buffer,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        Write(buffer.Span);
        return Task.CompletedTask;
    }

    public override Task WriteLineAsync(
        ReadOnlyMemory<char> buffer,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        WriteLine(buffer.Span);
        return Task.CompletedTask;
    }
#endif

    public override Task FlushAsync()
        => Task.CompletedTask;
}

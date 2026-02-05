using System.Globalization;
using System.Text;
using Cysharp.Text;

namespace ActualLab.IO.Internal;

/// <summary>
/// A <see cref="TextWriter"/> that writes UTF-8 encoded text
/// into a <see cref="Utf8ValueStringBuilder"/> buffer.
/// </summary>
public sealed class Utf8TextWriter(IFormatProvider formatProvider) : TextWriter(formatProvider)
{
    private Utf8ValueStringBuilder _buffer = ZString.CreateUtf8StringBuilder();

    public ref Utf8ValueStringBuilder Buffer => ref _buffer;
    public override Encoding Encoding => EncodingExt.Utf8NoBom;

    public Utf8TextWriter()
        : this(CultureInfo.CurrentCulture)
    { }

    protected override void Dispose(bool disposing)
    {
        _buffer.Dispose();
        base.Dispose(disposing);
    }

    public override string ToString()
        => _buffer.ToString();

    public void Clear()
        => _buffer.Clear();

    public void Renew(int maxCapacity)
    {
        if (_buffer.Length <= maxCapacity)
            _buffer.Clear();
        else {
            _buffer.Dispose();
            _buffer = ZString.CreateUtf8StringBuilder();
        }
    }

    public override void Close()
        => Dispose(true);

    public override Task FlushAsync()
        => Task.CompletedTask;

    // WriteXxx

    public void WriteLiteral(byte value)
    {
        var span = _buffer.GetSpan(1);
        span[0] = value;
        _buffer.Advance(1);
    }

    public void WriteLiteral(ReadOnlySpan<byte> bytes)
        => _buffer.AppendLiteral(bytes);

    public override void Write(char value)
        => _buffer.Append(value);

    public override void Write(char[] buffer, int index, int count)
    {
        if (buffer is null)
            throw new ArgumentNullException(nameof(buffer));
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (count < 0 || buffer.Length - index < count)
            throw new ArgumentOutOfRangeException(nameof(count));

        _buffer.Append(buffer.AsSpan(index, count));
    }

    public override void Write(string? value)
    {
        if (value is null)
            return;

        _buffer.Append(value);
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
        => _buffer.Append<bool>(value);

    public override void Write(decimal value)
        => _buffer.Append(value);

#if !NETSTANDARD2_0
    public override void Write(ReadOnlySpan<char> buffer)
        => _buffer.Append(buffer);

    public override void WriteLine(ReadOnlySpan<char> buffer)
    {
        _buffer.Append(buffer);
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
}

using System.Text;
using Cysharp.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ActualLab.Serialization.Internal;
using ActualLab.Text;
using CommunityToolkit.HighPerformance;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace ActualLab.Serialization;

#pragma warning disable CA2326, CA2327, CA2328
#pragma warning disable IL2026, IL2116

/// <summary>
/// An <see cref="ITextSerializer"/> implementation backed by Newtonsoft.Json (JSON.NET).
/// </summary>
public class NewtonsoftJsonSerializer : TextSerializerBase
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private readonly JsonSerializer _jsonSerializer;
    private static volatile NewtonsoftJsonSerializer? _default;
    private static volatile TypeDecoratingTextSerializer? _defaultTypeDecorating;

    public static JsonSerializerSettings DefaultSettings { get; set; } = new() {
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
        TypeNameHandling = TypeNameHandling.Auto,
        NullValueHandling = NullValueHandling.Ignore,
        ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
        DateParseHandling = DateParseHandling.None, // This makes sure all strings are deserialized as-is
        ContractResolver = new DefaultContractResolver(),
    };

    public static NewtonsoftJsonSerializer Default {
        get {
            if (_default is { } value)
                return value;
            lock (StaticLock)
                // ReSharper disable once NonAtomicCompoundOperator
                return _default ??= new(DefaultSettings);
        }
        set {
            lock (StaticLock)
                _default = value;
        }
    }

    public static TypeDecoratingTextSerializer DefaultTypeDecorating {
        get {
            if (_defaultTypeDecorating is { } value)
                return value;
            lock (StaticLock)
                // ReSharper disable once NonAtomicCompoundOperator
                return _defaultTypeDecorating ??= new TypeDecoratingTextSerializer(Default);
        }
        set {
            lock (StaticLock)
                _defaultTypeDecorating = value;
        }
    }

    // Instance members

    public JsonSerializerSettings Settings { get; }

    public NewtonsoftJsonSerializer(JsonSerializerSettings? settings = null)
    {
        Settings = settings ??= DefaultSettings;
        _jsonSerializer = JsonSerializer.Create(settings);
    }

    // Read

    public override object? Read(string data, Type type)
    {
        var stringReader = new StringReader(data); // No need to dispose
        var reader = new JsonTextReader(stringReader) { CloseInput = false }; // No need to dispose
        return _jsonSerializer.Deserialize(reader, type);
    }

    public override object? Read(ReadOnlyMemory<byte> data, Type type, out int readLength)
    {
        var textReader = new Utf8PositionTrackingTextReader(data);
        var reader = new JsonTextReader(textReader) { CloseInput = false }; // No need to dispose
        var result = _jsonSerializer.Deserialize(reader, type);
        readLength = textReader.GetBytePosition(reader.LineNumber, reader.LinePosition);
        return result;
    }

    // Write

    public override string Write(object? value, Type type)
    {
        using var stringWriter = new ZStringWriter();
        using var writer = new JsonTextWriter(stringWriter) { CloseOutput = false };
        writer.Formatting = _jsonSerializer.Formatting;
        // ReSharper disable once HeapView.BoxingAllocation
        _jsonSerializer.Serialize(writer, value, type);
        return stringWriter.ToString();
    }

    public override void Write(TextWriter textWriter, object? value, Type type)
    {
        using var writer = new JsonTextWriter(textWriter) { CloseOutput = false };
        writer.Formatting = _jsonSerializer.Formatting;
        // ReSharper disable once HeapView.BoxingAllocation
        _jsonSerializer.Serialize(writer, value, type);
    }

#if NETSTANDARD2_0
    private sealed unsafe class Utf8PositionTrackingTextReader : TextReader
#else
    private sealed class Utf8PositionTrackingTextReader : TextReader
#endif
    {
        private readonly ReadOnlyMemory<byte> _data;
        private int _bytePosition;
        private int _lineNumber = 1;
        private int _linePosition;
        private bool _isAfterCarriageReturn;
        private char[]? _lastBuffer;
        private int _lastBufferIndex;
        private int _lastBufferCount;
        private int _lastBufferBytePosition;
        private int _lastBufferLineNumber;
        private int _lastBufferLinePosition;
        private bool _lastBufferIsAfterCarriageReturn;

        public Utf8PositionTrackingTextReader(ReadOnlyMemory<byte> data)
        {
            _data = data;
            var span = data.Span;
            if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
                _bytePosition = 3;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            if (count == 0 || _bytePosition >= _data.Length)
                return 0;

            var source = _data.Span[_bytePosition..];
            if (source.Length > count) {
                var sourceLength = count;
                while (sourceLength > 0 && (source[sourceLength] & 0xC0) == 0x80)
                    sourceLength--;
                source = source[..sourceLength];
            }
            var charsUsed = 0;
#if NETSTANDARD2_0
            fixed (byte* sourcePtr = &source.GetPinnableReference())
            fixed (char* targetPtr = &buffer[index])
                charsUsed = EncodingExt.Utf8NoBom.GetChars(sourcePtr, source.Length, targetPtr, count);
#else
            charsUsed = EncodingExt.Utf8NoBom.GetChars(source, buffer.AsSpan(index, count));
#endif
            _lastBuffer = buffer;
            _lastBufferIndex = index;
            _lastBufferCount = charsUsed;
            _lastBufferBytePosition = _bytePosition;
            _lastBufferLineNumber = _lineNumber;
            _lastBufferLinePosition = _linePosition;
            _lastBufferIsAfterCarriageReturn = _isAfterCarriageReturn;
            _bytePosition += source.Length;
            AdvancePosition(buffer.AsSpan(index, charsUsed));
            return charsUsed;
        }

        public int GetBytePosition(int lineNumber, int linePosition)
        {
            if (_lastBuffer is null)
                return _bytePosition;

            var relativeLinePosition = linePosition - _lastBufferLinePosition;
            if (lineNumber == _lastBufferLineNumber
                && relativeLinePosition >= 0
                && relativeLinePosition <= _lastBufferCount) {
                var prefix = _lastBuffer.AsSpan(_lastBufferIndex, relativeLinePosition);
                if (prefix.IndexOfAny('\r', '\n') < 0)
                    return _lastBufferBytePosition
                        + EncodingExt.Utf8NoBom.GetByteCount(_lastBuffer, _lastBufferIndex, relativeLinePosition);
            }

            var currentLineNumber = _lastBufferLineNumber;
            var currentLinePosition = _lastBufferLinePosition;
            var isAfterCarriageReturn = _lastBufferIsAfterCarriageReturn;
            for (var i = 0; i <= _lastBufferCount; i++) {
                if (currentLineNumber == lineNumber && currentLinePosition == linePosition)
                    return _lastBufferBytePosition
                        + EncodingExt.Utf8NoBom.GetByteCount(_lastBuffer, _lastBufferIndex, i);
                if (i == _lastBufferCount)
                    break;

                AdvancePosition(_lastBuffer[_lastBufferIndex + i],
                    ref currentLineNumber, ref currentLinePosition, ref isAfterCarriageReturn);
            }
            throw new InvalidOperationException("The JSON reader position isn't in its latest input buffer.");
        }

        private void AdvancePosition(ReadOnlySpan<char> source)
        {
            var lineBreakIndex = source.IndexOfAny('\r', '\n');
            if (lineBreakIndex < 0) {
                _linePosition += source.Length;
                _isAfterCarriageReturn = false;
                return;
            }

            _linePosition += lineBreakIndex;
            foreach (var c in source[lineBreakIndex..])
                AdvancePosition(c, ref _lineNumber, ref _linePosition, ref _isAfterCarriageReturn);
        }

        private static void AdvancePosition(
            char c, ref int lineNumber, ref int linePosition, ref bool isAfterCarriageReturn)
        {
            if (c == '\r') {
                lineNumber++;
                linePosition = 0;
                isAfterCarriageReturn = true;
            }
            else if (c == '\n') {
                if (!isAfterCarriageReturn)
                    lineNumber++;
                linePosition = 0;
                isAfterCarriageReturn = false;
            }
            else {
                linePosition++;
                isAfterCarriageReturn = false;
            }
        }
    }
}

using System.Buffers;
using System.Buffers.Text;

namespace ActualLab.Text;

public static class Base64Encode
{
    private const int MaxOnStackLength = 2048;
    private const char EqualsChar = '=';
    private const byte EqualsByte = (byte)EqualsChar;
    private const char ForwardSlashChar = '/';
    private const char PlusChar = '+';
    private const char UnderscoreChar = '_';
    private const char DashChar = '-';

    public static string Encode(ReadOnlySpan<byte> source)
    {
        var length = Base64.GetMaxEncodedToUtf8Length(source.Length);
        if (length == 0)
            return "";

        Span<char> chars = length <= MaxOnStackLength ? stackalloc char[length] : new char[length];
        var bytes = MemoryMarshal.Cast<char, byte>(chars);
        Base64.EncodeToUtf8(source, bytes, out _, out length);

        for (var i = length - 1; i >= 0; i--) {
            var c = (char)bytes[i];
            switch (c) {
            case ForwardSlashChar:
                c = DashChar;
                break;
            case PlusChar:
                c = UnderscoreChar;
                break;
            }
            chars[i] = c;
        }

        var trimmedChars = chars[length - 2] == EqualsChar ? chars[..(length - 2)]
            : chars[length - 1] == EqualsChar ? chars[..(length - 1)]
            : chars;
#if !NETSTANDARD2_0
        return new string(trimmedChars);
#else
        return trimmedChars.ToString();
#endif
    }

    public static Span<byte> Decode(string source)
    {
        if (source.Length == 0)
            return Span<byte>.Empty;

        var fullLength = ((source.Length + 3) >> 2) << 2;
        Span<byte> utf8 = fullLength <= MaxOnStackLength ? stackalloc byte[fullLength] : new byte[fullLength];
        for (var i = 0; i < source.Length; i++) {
            var c = source[i];
            switch (c) {
            case DashChar:
                c = ForwardSlashChar;
                break;
            case UnderscoreChar:
                c = PlusChar;
                break;
            }
            utf8[i] = (byte)c;
        }
        for (var i = source.Length; i < utf8.Length; i++)
            utf8[i] = EqualsByte;

        var bytes = new byte[Base64.GetMaxDecodedFromUtf8Length(fullLength)];
        var status = Base64.DecodeFromUtf8(utf8, bytes, out _, out var length);
        if (status != OperationStatus.Done)
            throw new ArgumentOutOfRangeException(nameof(source));

        return bytes.AsSpan(0, length);
    }
}

using System.Security.Cryptography;
using ActualLab.IO;

namespace ActualLab.Generators;

// Thread-safe!

/// <summary>
/// A thread-safe generator that produces random strings
/// from a configurable alphabet using cryptographic randomness.
/// </summary>
public class RandomStringGenerator : Generator<string>, IDisposable
{
    public static readonly string DefaultAlphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ-_";
    public static readonly string Base64Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ+/";
    public static readonly string Base32Alphabet = "0123456789abcdefghijklmnopqrstuv";
    public static readonly string Base16Alphabet = "0123456789abcdef";
    public static readonly RandomStringGenerator Default = new();

    protected readonly RandomNumberGenerator Rng;
    // ReSharper disable once InconsistentlySynchronizedField
    protected object Lock => Rng;

    public string Alphabet { get; }
    public int Length { get; }

    public RandomStringGenerator(int length = 16, string? alphabet = null, RandomNumberGenerator? rng = null)
    {
        if (length < 1)
            throw new ArgumentOutOfRangeException(nameof(length));
        alphabet ??= DefaultAlphabet;
        if (alphabet.Length < 1)
            throw new ArgumentOutOfRangeException(nameof(alphabet));
        rng ??= RandomNumberGenerator.Create();

        Length = length;
        Alphabet = alphabet;
        Rng = rng;
    }

    public void Dispose() => Rng.Dispose();

    public override string Next() => Next(Length);

    private static void FillInCharSpan(Span<char> charSpan, string alphabet, ReadOnlySpan<byte> bufferSpan)
    {
        var alphabetSpan = alphabet.AsSpan();
        var alphabetLength = alphabetSpan.Length;
        if (Bits.IsPowerOf2((ulong)alphabetLength)) {
            var alphabetMask = alphabetLength - 1;
            for (var i = 0; i<charSpan.Length; i++)
                charSpan[i] = alphabetSpan[bufferSpan[i] & alphabetMask];
        }
        else {
            for (var i = 0; i<charSpan.Length; i++)
                charSpan[i] = alphabetSpan[bufferSpan[i] % alphabetLength];
        }
    }

    public string Next(int length, string? alphabet = null)
    {
        if (alphabet is null)
            alphabet = Alphabet;
        else if (alphabet.Length < 1)
            throw new ArgumentOutOfRangeException(nameof(alphabet));
#if !NETSTANDARD2_0
        var buffer = new RefArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, length, mustClear: false);
        try {
            var span = buffer.GetSpan(length);
            lock (Lock)
                Rng.GetBytes(span);
            return string.Create(length, (buffer.Array, length, alphabet), static (charSpan, arg) => {
                var (array, len, alphabet1) = arg;
                FillInCharSpan(charSpan, alphabet1!, array.AsSpan(0, len));
            });
        }
        finally {
            buffer.Release();
        }
#else
        var byteBuffer = ArrayPools.SharedBytePool.Rent(length);
        var charBuffer = ArrayPools.SharedCharPool.Rent(length);
        try {
            lock (Lock) {
                Rng.GetBytes(byteBuffer, 0, length);
            }
            var charSpan = charBuffer.AsSpan(0, length);
            FillInCharSpan(charSpan, alphabet, byteBuffer.AsSpan());
            return new string(charBuffer, 0, length);
        }
        finally {
            ArrayPools.SharedCharPool.Return(charBuffer);
            ArrayPools.SharedBytePool.Return(byteBuffer);
        }
#endif
    }
}

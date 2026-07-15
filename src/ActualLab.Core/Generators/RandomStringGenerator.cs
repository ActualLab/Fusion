using System.Buffers.Binary;
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

    private static void FillPowerOfTwoCharSpan(Span<char> charSpan, string alphabet, ReadOnlySpan<byte> bufferSpan)
    {
        var alphabetSpan = alphabet.AsSpan();
        var alphabetMask = alphabetSpan.Length - 1;
        for (var i = 0; i < charSpan.Length; i++)
            charSpan[i] = alphabetSpan[bufferSpan[i] & alphabetMask];
    }

    private static void FillUnbiasedCharSpan(
        Span<char> charSpan,
        string alphabet,
        RandomNumberGenerator rng,
        byte[] buffer)
    {
        var alphabetSpan = alphabet.AsSpan();
        var alphabetLength = alphabetSpan.Length;
        var outputIndex = 0;
        if (alphabetLength <= 256) {
            var threshold = 256 % alphabetLength;
            while (outputIndex < charSpan.Length) {
                FillRandomBytes(rng, buffer);
                foreach (var sample in buffer) {
                    if (sample < threshold)
                        continue;
                    charSpan[outputIndex++] = alphabetSpan[sample % alphabetLength];
                    if (outputIndex == charSpan.Length)
                        return;
                }
            }
            return;
        }

        var uintAlphabetLength = (uint)alphabetLength;
        var isPowerOfTwo = Bits.IsPowerOf2(uintAlphabetLength);
        var alphabetMask = uintAlphabetLength - 1;
        var threshold32 = isPowerOfTwo ? 0 : unchecked(0U - uintAlphabetLength) % uintAlphabetLength;
        while (outputIndex < charSpan.Length) {
            FillRandomBytes(rng, buffer);
            var bufferSpan = buffer.AsSpan();
            for (var offset = 0; offset <= bufferSpan.Length - sizeof(uint); offset += sizeof(uint)) {
                var sample = BinaryPrimitives.ReadUInt32LittleEndian(bufferSpan[offset..]);
                if (sample < threshold32)
                    continue;
                var alphabetIndex = isPowerOfTwo ? sample & alphabetMask : sample % uintAlphabetLength;
                charSpan[outputIndex++] = alphabetSpan[(int)alphabetIndex];
                if (outputIndex == charSpan.Length)
                    return;
            }
        }
    }

    private static void FillRandomBytes(RandomNumberGenerator rng, byte[] buffer)
    {
        lock (rng) {
#if !NETSTANDARD2_0
            rng.GetBytes(buffer.AsSpan());
#else
            rng.GetBytes(buffer, 0, buffer.Length);
#endif
        }
    }

    public string Next(int length, string? alphabet = null)
    {
        if (alphabet is null)
            alphabet = Alphabet;
        else if (alphabet.Length < 1)
            throw new ArgumentOutOfRangeException(nameof(alphabet));
#if !NETSTANDARD2_0
        var alphabetLength = alphabet.Length;
        var useByteMask = alphabetLength <= 256 && Bits.IsPowerOf2((ulong)alphabetLength);
        var buffer = new RefArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, Math.Max(length, sizeof(uint)), mustClear: false);
        try {
            if (useByteMask) {
                var span = buffer.Array.AsSpan(0, length);
                lock (Lock)
                    Rng.GetBytes(span);
                return string.Create(length, (buffer.Array, length, alphabet), static (charSpan, arg) => {
                    var (array, len, alphabet1) = arg;
                    FillPowerOfTwoCharSpan(charSpan, alphabet1!, array.AsSpan(0, len));
                });
            }
            return string.Create(length, (buffer.Array, alphabet, Rng), static (charSpan, arg) => {
                var (array, alphabet1, rng) = arg;
                FillUnbiasedCharSpan(charSpan, alphabet1, rng, array);
            });
        }
        finally {
            buffer.Release();
        }
#else
        var alphabetLength = alphabet.Length;
        var useByteMask = alphabetLength <= 256 && Bits.IsPowerOf2((ulong)alphabetLength);
        var byteBuffer = ArrayPools.SharedBytePool.Rent(Math.Max(length, sizeof(uint)));
        var charBuffer = ArrayPools.SharedCharPool.Rent(length);
        try {
            var charSpan = charBuffer.AsSpan(0, length);
            if (useByteMask) {
                lock (Lock)
                    Rng.GetBytes(byteBuffer, 0, length);
                FillPowerOfTwoCharSpan(charSpan, alphabet, byteBuffer.AsSpan(0, length));
            }
            else
                FillUnbiasedCharSpan(charSpan, alphabet, Rng, byteBuffer);
            return new string(charBuffer, 0, length);
        }
        finally {
            ArrayPools.SharedCharPool.Return(charBuffer);
            ArrayPools.SharedBytePool.Return(byteBuffer);
        }
#endif
    }
}

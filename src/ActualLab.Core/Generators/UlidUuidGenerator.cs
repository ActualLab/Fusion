using System.Security.Cryptography;

namespace ActualLab.Generators;

/// <summary>
/// A <see cref="UuidGenerator"/> that produces ULID strings: a 48-bit Unix time in milliseconds
/// followed by 80 random bits, in Crockford's Base32 (26 chars). It's the algorithm of
/// <c>Ulid.NewUlid().ToString()</c> from the Ulid package, which Core no longer references.
/// </summary>
public class UlidUuidGenerator : UuidGenerator
{
    private const string Base32Chars = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int Length = 26;

    [ThreadStatic] private static ulong _randomState;

    public static readonly UlidUuidGenerator Instance = new();

    public override string Next()
    {
        var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var random0 = NextRandom();
        var random1 = NextRandom();
        // The 128-bit ULID as two big-endian halves: timestamp (48 bits) + randomness (80 bits)
        var hi = (timestamp << 16) | (random0 >> 48);
        var lo = (random0 << 16) | (random1 & 0xFFFF);
#if NETCOREAPP3_1_OR_GREATER
        return string.Create(Length, (hi, lo), static (chars, state) => Format(chars, state.hi, state.lo));
#else
        var chars = new char[Length];
        Format(chars, hi, lo);
        return new string(chars);
#endif
    }

    // Private methods

    // Each char encodes 5 bits; 26 chars hold 130 bits, so the first one encodes just the top 3 bits
    private static void Format(Span<char> chars, ulong hi, ulong lo)
    {
        for (var i = Length - 1; i >= 0; i--) {
            chars[i] = Base32Chars[(int)(lo & 31)];
            lo = (lo >> 5) | (hi << 59);
            hi >>= 5;
        }
    }

    // XorShift64 with a per-thread state seeded by RandomNumberGenerator, as Ulid's RandomProvider does
    private static ulong NextRandom()
    {
        var x = _randomState;
        if (x == 0) {
            var seed = new byte[8];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(seed);
            x = BitConverter.ToUInt64(seed, 0);
            if (x == 0)
                x = 88172645463325252UL;
        }
        x ^= x << 7;
        x ^= x >> 9;
        _randomState = x;
        return x;
    }
}

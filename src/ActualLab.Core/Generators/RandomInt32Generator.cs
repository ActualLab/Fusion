using System.Security.Cryptography;

namespace ActualLab.Generators;

/// <summary>
/// A thread-safe generator that produces cryptographically random <see cref="int"/> values.
/// </summary>
// Thread-safe!
public sealed class RandomInt32Generator(RandomNumberGenerator? rng = null) : Generator<int>
{
    private readonly byte[] _buffer = new byte[sizeof(int)];
    private readonly RandomNumberGenerator _rng = rng ?? RandomNumberGenerator.Create();

    public override int Next()
    {
        lock (_rng)
            _rng.GetBytes(_buffer);
        var bufferSpan = MemoryMarshal.Cast<byte, int>(_buffer.AsSpan());
        return bufferSpan![0];
    }
}

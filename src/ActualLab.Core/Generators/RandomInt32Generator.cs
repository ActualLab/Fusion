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
        // _buffer is shared, so it must be read before the lock is released - otherwise two
        // concurrent callers can overwrite each other's bytes and return the same value
        lock (_rng) {
            _rng.GetBytes(_buffer);
            return MemoryMarshal.Cast<byte, int>(_buffer.AsSpan())[0];
        }
    }
}

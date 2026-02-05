namespace ActualLab.Mathematics;

/// <summary>
/// A sieve of Eratosthenes implementation for computing and querying prime numbers.
/// </summary>
public sealed class PrimeSieve
{
    public static ReadOnlySpan<int> PrecomputedPrimes => [
        1, 3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
        1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
        17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
        187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
        1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369,
    ];

#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static volatile PrimeSieve? _instance;

    private readonly int _limitSqrt;
    private readonly BitArray _isPrime;

    public int Limit { get; }

    public PrimeSieve(int limit = 10010896)
    {
        _limitSqrt = (int)(1 + Math.Sqrt(limit));
        Limit = _limitSqrt * _limitSqrt;
        _isPrime = new BitArray(1 + (Limit / 2), true);
        Compute();
    }

    public bool IsPrime(int n) => (n&1) != 0 && _isPrime[n >> 1];

    private void Compute()
    {
        var limit = Limit;
        var limitSqrt = _limitSqrt;
        for (var i = 3; i < limitSqrt; i += 2) {
            if (_isPrime[i >> 1]) {
                var k = i << 1;
                for (var j = i * i; j < limit; j += k)
                    _isPrime[j >> 1] = false;
            }
        }
    }

    public static int GetPrecomputedPrime(int minPrime)
    {
        foreach (var prime in PrecomputedPrimes) {
            if (prime >= minPrime)
                return prime;
        }
        throw new ArgumentOutOfRangeException(nameof(minPrime));
    }

    public static PrimeSieve GetOrCompute(int limit = 10010896)
    {
        if (_instance?.Limit >= limit)
            return _instance;

        lock (StaticLock) {
            if (_instance?.Limit >= limit)
                return _instance;

            _instance = new PrimeSieve(limit);
            return _instance;
        }
    }
}

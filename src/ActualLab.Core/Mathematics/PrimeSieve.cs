namespace ActualLab.Mathematics;

public class PrimeSieve
{
#if NET9_0_OR_GREATER
    private static readonly Lock Lock = new();
#else
    private static readonly object Lock = new();
#endif
    private static PrimeSieve? _instance;

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

    public static PrimeSieve GetOrCompute(int limit)
    {
        if (_instance?.Limit >= limit)
            return _instance;

        lock (Lock) {
            if (_instance?.Limit >= limit)
                return _instance;

            _instance = new PrimeSieve(limit);
            return _instance;
        }
    }
}

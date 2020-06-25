using System;
using System.Collections;

namespace Stl.Mathematics
{
    public class PrimeSieve
    {
        private readonly int _limit;
        private readonly int _limitSqrt;
        private readonly BitArray _isPrime;
        
        public PrimeSieve(int limit = 10010896)
        {
            _limitSqrt = (int) (1 + System.Math.Sqrt(limit));
            _limit = _limitSqrt * _limitSqrt;
            _isPrime = new BitArray(1 + _limit / 2, true);
            Compute();
        }

        public bool IsPrime(int n) => (n&1) != 0 && _isPrime[n >> 1];

        private void Compute()
        {
            var limit = _limit;
            var limitSqrt = _limitSqrt;
            for (var i = 3; i < limitSqrt; i += 2) {
                if (_isPrime[i >> 1]) {
                    var k = i << 1;
                    for (var j = i * i; j < limit; j += k)
                        _isPrime[j >> 1] = false;
                }
            }
        }
    }
}

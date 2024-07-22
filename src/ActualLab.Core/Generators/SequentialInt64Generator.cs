namespace ActualLab.Generators;

public sealed class SequentialInt64Generator(long start = 1) : Generator<long>
{
    private long _counter = start - 1;

    public override long Next()
        => Interlocked.Increment(ref _counter);
}

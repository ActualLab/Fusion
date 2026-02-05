namespace ActualLab.Generators;

/// <summary>
/// A thread-safe generator that produces sequentially incrementing <see cref="int"/> values.
/// </summary>
public sealed class SequentialInt32Generator(int start = 1) : Generator<int>
{
    private int _counter = start - 1;

    public override int Next()
        => Interlocked.Increment(ref _counter);
}

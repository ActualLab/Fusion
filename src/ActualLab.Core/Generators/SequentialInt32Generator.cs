namespace ActualLab.Generators;

public sealed class SequentialInt32Generator(int start = 1) : Generator<int>
{
    private int _counter = start - 1;

    public override int Next()
        => Interlocked.Increment(ref _counter);
}

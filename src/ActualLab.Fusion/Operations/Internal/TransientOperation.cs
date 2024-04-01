using System.Globalization;

namespace ActualLab.Fusion.Operations.Internal;

public class TransientOperation : Operation
{
    private static long _lastId;

    public TransientOperation()
        => Id = "Local-" + Interlocked.Increment(ref _lastId).ToString(CultureInfo.InvariantCulture);
}

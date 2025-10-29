using System.Globalization;
using ActualLab.Interception;

namespace ActualLab.Fusion.Interception;

#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()

public sealed class ComputeMethodInput : ComputedInput, IEquatable<ComputeMethodInput>
{
    public readonly ComputeMethodDef MethodDef;
    public readonly Invocation Invocation;

    public override bool IsDisposed
        => MethodDef.IsOfHasDisposableStatusType && Invocation.Proxy is IHasDisposeStatus { IsDisposed: true };

    public ComputeMethodInput(IComputeFunction function, ComputeMethodDef methodDef, Invocation invocation)
    {
        MethodDef = methodDef;
        Invocation = invocation;

        var arguments = invocation.Arguments;
        var hashCode = methodDef.Id
            + invocation.Proxy.GetHashCode()
            + arguments.GetHashCode(methodDef.CancellationTokenIndex);
        Initialize(function, hashCode);
    }

    public override string ToString()
        => string.Concat(
            Category,
            Invocation.Arguments.ToString(),
            "-Hash=",
            HashCode.ToString(CultureInfo.InvariantCulture));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ComputedOptions GetComputedOptions()
        => MethodDef.ComputedOptions;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Computed? GetExistingComputed()
        => ComputedRegistry.Get(this);

    internal ValueTask<object?> InvokeInterceptedUntyped(CancellationToken cancellationToken)
    {
        var ctIndex = MethodDef.CancellationTokenIndex;
        ValueTask<object?> resultTask;

        if (ctIndex < 0)
            resultTask = MethodDef.InterceptedObjectAsyncInvoker.Invoke(Invocation);
        else {
            Invocation.Arguments.SetCancellationToken(ctIndex, cancellationToken);
            try {
                resultTask = MethodDef.InterceptedObjectAsyncInvoker.Invoke(Invocation);
            }
            finally {
                Invocation.Arguments.SetCancellationToken(ctIndex, default); // Otherwise it may cause memory leak
            }
        }
        return resultTask;
    }

    // Equality

    public bool Equals(ComputeMethodInput? other)
    {
        if (other is null || HashCode != other.HashCode || !ReferenceEquals(MethodDef, other.MethodDef))
            return false;

        var invocation = Invocation;
        var otherInvocation = other.Invocation;
        return ReferenceEquals(invocation.Proxy, otherInvocation.Proxy)
            && invocation.Arguments.Equals(otherInvocation.Arguments, MethodDef.CancellationTokenIndex);
    }

    public override bool Equals(ComputedInput? obj)
        => obj is ComputeMethodInput other && Equals(other);
    public override bool Equals(object? obj)
        => obj is ComputeMethodInput other && Equals(other);
}

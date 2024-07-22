using Cysharp.Text;
using ActualLab.Interception;

namespace ActualLab.Fusion.Interception;

#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()

public sealed class ComputeMethodInput : ComputedInput, IEquatable<ComputeMethodInput>
{
    public readonly ComputeMethodDef MethodDef;
    public readonly Invocation Invocation;

    public override bool IsDisposed
        => MethodDef.IsDisposable && Invocation.Proxy is IHasIsDisposed { IsDisposed: true };

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
        => ZString.Concat(Category, Invocation.Arguments, "-Hash=", HashCode);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ComputedOptions GetComputedOptions()
        => MethodDef.ComputedOptions;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Computed? GetExistingComputed()
        => ComputedRegistry.Instance.Get(this);

    // Equality

    public bool Equals(ComputeMethodInput? other)
    {
        if (other == null || HashCode != other.HashCode || !ReferenceEquals(MethodDef, other.MethodDef))
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

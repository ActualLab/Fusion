using Cysharp.Text;
using ActualLab.Interception;

namespace ActualLab.Fusion.Interception;

public sealed class ComputeMethodInput : ComputedInput, IEquatable<ComputeMethodInput>
{
    public readonly ComputeMethodDef MethodDef;
    public readonly Invocation Invocation;

    // Shortcuts
    public object Service {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Invocation.Proxy;
    }

    public ArgumentList Arguments {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Invocation.Arguments;
    }

    public override bool IsDisposed
        => MethodDef.IsDisposable && Service is IHasIsDisposed { IsDisposed: true };

    public ComputeMethodInput(IComputeFunction function, ComputeMethodDef methodDef, Invocation invocation)
    {
        MethodDef = methodDef;
        Invocation = invocation;

        var arguments = invocation.Arguments;
        var hashCode = methodDef.GetHashCode()
            ^ invocation.Proxy.GetHashCode()
            ^ arguments.GetHashCode(methodDef.CancellationTokenIndex);
        Initialize(function, hashCode);
    }

    public override string ToString()
        => ZString.Concat(Category, Arguments, "-Hash=", HashCode);

    public override ComputedOptions GetComputedOptions()
        => MethodDef.ComputedOptions;

    public override IComputed? GetExistingComputed()
        => ComputedRegistry.Instance.Get(this);

    // Equality

    public bool Equals(ComputeMethodInput? other)
        => other != null
        && HashCode == other.HashCode
        && ReferenceEquals(MethodDef, other.MethodDef)
        && ReferenceEquals(Service, other.Service)
        && Arguments.Equals(other.Arguments, MethodDef.CancellationTokenIndex);

    public override bool Equals(ComputedInput? obj)
        => obj is ComputeMethodInput other && Equals(other);
    public override bool Equals(object? obj)
        => obj is ComputeMethodInput other && Equals(other);
    public override int GetHashCode()
        => HashCode;
}

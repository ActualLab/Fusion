using System.Globalization;
using ActualLab.Interception;

namespace ActualLab.Fusion.Interception;

#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()

/// <summary>
/// A <see cref="ComputedInput"/> representing the arguments of an intercepted compute method call.
/// </summary>
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

        Initialize(function, ComputeHashCode(methodDef, invocation));
    }

#if NET9_0_OR_GREATER
    private ComputeMethodInput(in Lookup lookup)
    {
        MethodDef = lookup.MethodDef;
        Invocation = lookup.Invocation;
        Initialize(lookup.Function, lookup.HashCode);
    }
#endif

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

    // Internal helpers

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
                Invocation.Arguments.SetCancellationToken(ctIndex, default); // Otherwise it may cause a memory leak
            }
        }
        return resultTask;
    }

    // Private methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeHashCode(ComputeMethodDef methodDef, Invocation invocation)
        => methodDef.Id
            + invocation.Proxy.GetHashCode()
            + invocation.Arguments.GetHashCode(methodDef.CancellationTokenIndex);

#if NET9_0_OR_GREATER
    // Nested types

    internal readonly struct Lookup
    {
        public readonly IComputeFunction Function;
        public readonly ComputeMethodDef MethodDef;
        public readonly Invocation Invocation;
        public readonly int HashCode;

        public Lookup(IComputeFunction function, ComputeMethodDef methodDef, Invocation invocation)
        {
            Function = function;
            MethodDef = methodDef;
            Invocation = invocation;

            HashCode = ComputeHashCode(methodDef, invocation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EqualsInput(ComputeMethodInput other)
        {
            if (HashCode != other.HashCode || !ReferenceEquals(MethodDef, other.MethodDef))
                return false;

            var invocation = Invocation;
            var otherInvocation = other.Invocation;
            return ReferenceEquals(invocation.Proxy, otherInvocation.Proxy)
                && invocation.Arguments.Equals(otherInvocation.Arguments, MethodDef.CancellationTokenIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComputeMethodInput ToInput()
            => new(in this);
    }
#endif
}

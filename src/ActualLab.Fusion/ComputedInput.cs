using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

#pragma warning disable CA1721

public abstract class ComputedInput : IEquatable<ComputedInput>, IHasIsDisposed
{
    public static IEqualityComparer<ComputedInput> EqualityComparer { get; } = new EqualityComparerImpl();

    public IComputeFunction Function { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; private set; } = null!;
    public int HashCode { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; private set; }

    public virtual string Category {
        get => Function.ToString() ?? "";
        init => throw Errors.ComputedInputCategoryCannotBeSet();
    }

    public virtual bool IsDisposed => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Initialize(IComputeFunction function, int hashCode)
    {
        Function = function;
        HashCode = hashCode;
    }

    public override string ToString()
        => $"{Category}-Hash={HashCode}";

    public abstract ComputedOptions GetComputedOptions();
    public abstract Computed? GetExistingComputed();

    // Equality

    public abstract bool Equals(ComputedInput? other);
    public override bool Equals(object? obj)
        => obj is ComputedInput other && Equals(other);

    // ReSharper disable once NonReadonlyMemberInGetHashCode
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sealed override int GetHashCode()
        => HashCode;

    public static bool operator ==(ComputedInput? left, ComputedInput? right)
        => Equals(left, right);
    public static bool operator !=(ComputedInput? left, ComputedInput? right)
        => !Equals(left, right);

    // Equality comparer

    private sealed class EqualityComparerImpl : IEqualityComparer<ComputedInput>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ComputedInput? x, ComputedInput? y)
            => !ReferenceEquals(x, null) && x.Equals(y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(ComputedInput obj)
            => obj.HashCode;
    }

}

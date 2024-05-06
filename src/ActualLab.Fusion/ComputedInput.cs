using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

public abstract class ComputedInput : IEquatable<ComputedInput>, IHasIsDisposed
{
    public IFunction Function { get; private set; } = null!;
#pragma warning disable CA1721
    public int HashCode { get; private set; }
#pragma warning restore CA1721
    public virtual string Category {
        get => Function.ToString() ?? "";
        init => throw Errors.ComputedInputCategoryCannotBeSet();
    }
    public virtual bool IsDisposed => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Initialize(IFunction function, int hashCode)
    {
        Function = function;
        HashCode = hashCode;
    }

    public override string ToString()
        => $"{Category}-Hash={HashCode}";

    public abstract IComputed? GetExistingComputed();

    // Equality

    public abstract bool Equals(ComputedInput? other);
    public override bool Equals(object? obj)
        => obj is ComputedInput other && Equals(other);

    // ReSharper disable once NonReadonlyMemberInGetHashCode
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode;

    public static bool operator ==(ComputedInput? left, ComputedInput? right)
        => Equals(left, right);
    public static bool operator !=(ComputedInput? left, ComputedInput? right)
        => !Equals(left, right);
}

namespace ActualLab.Fusion;

/// <summary>
/// Declares the <see cref="InvalidationMode"/> of a single <c>[CommandHandler]</c> method
/// or of every command handler declared by a compute service type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method)]
public sealed class InvalidationModeAttribute(InvalidationMode mode) : Attribute
{
    public InvalidationMode Mode { get; } = mode;
}

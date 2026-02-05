namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// Defines flags controlling when and how a <see cref="Computed"/> is invalidated.
/// </summary>
[Flags]
public enum InvalidationFlags
{
    InvalidateOnSetOutput = 0x100,
    InvalidateOnSetOutputImmediately = 0x200,
    DelayedInvalidationStarted = 0x400,
}

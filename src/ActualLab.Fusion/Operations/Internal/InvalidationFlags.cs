namespace ActualLab.Fusion.Operations.Internal;

[Flags]
public enum InvalidationFlags
{
    InvalidateOnSetOutput = 0x100,
    InvalidateOnSetOutputImmediately = 0x200,
    DelayedInvalidationStarted = 0x400,
}

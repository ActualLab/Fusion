namespace ActualLab.Fusion.Operations.Internal;

[Flags]
public enum ComputedFlags
{
    InvalidateOnSetOutput = 1,
    InvalidateOnSetOutputImmediately = 2,
    DelayedInvalidationStarted = 4,
}

namespace ActualLab.Fusion;

/// <summary>
/// Describes how a command handler declares its invalidations and how far they reach.
/// See <see cref="InvalidationModeAttribute"/> for how it's declared.
/// </summary>
public enum InvalidationMode
{
    // The order matters: anything but Legacy disqualifies the command from the replay-based
    // invalidation pass, and the default value must be the safe (no replay) one.
    None = 0,
    Legacy = 1,
    Local = 2,
    Replicated = 3,
}

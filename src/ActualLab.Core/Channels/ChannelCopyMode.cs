namespace ActualLab.Channels;

/// <summary>
/// Defines which channel state transitions to propagate when copying between channels.
/// </summary>
[Flags]
public enum ChannelCopyMode
{
    CopyCompletion = 1,
    CopyError = 2,
    CopyCancellation = 4,
    CopyAll = CopyCompletion + CopyError + CopyCancellation,
    Silently = 64,
    CopyAllSilently = CopyAll + Silently,
}

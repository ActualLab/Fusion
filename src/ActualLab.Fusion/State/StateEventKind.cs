namespace ActualLab.Fusion;

/// <summary>
/// Defines the kinds of lifecycle events raised by a <see cref="State"/>.
/// </summary>
[Flags]
public enum StateEventKind
{
    Invalidated = 1,
    Updating = 2,
    Updated = 4,
    All = Invalidated | Updating | Updated,
}

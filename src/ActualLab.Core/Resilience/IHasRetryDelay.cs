namespace ActualLab.Resilience;

/// <summary>
/// Defines the contract for errors that specify how long to wait before retrying.
/// </summary>
/// <remarks>
/// A cached <see cref="Computed"/> error is auto-invalidated after a delay that depends on
/// the error's <see cref="Transiency"/>. When the error implements this interface, that delay
/// is extended to at least <see cref="RetryDelay"/>, so a retry can't be issued earlier than
/// the error itself asks for. This matters for errors whose very cause is request volume:
/// invalidating early triggers a retry, and the retry makes the underlying condition worse.
/// </remarks>
public interface IHasRetryDelay
{
    public TimeSpan RetryDelay { get; }
}

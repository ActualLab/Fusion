namespace ActualLab.Fusion;

/// <summary>
/// Validates whether a given <see cref="Session"/> is acceptable for use.
/// </summary>
public interface ISessionValidator
{
    public Task<bool> IsValidSession(Session session, CancellationToken cancellationToken = default);
}

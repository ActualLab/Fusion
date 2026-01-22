namespace ActualLab.Fusion;

public interface ISessionValidator
{
    public Task<bool> IsValidSession(Session session, CancellationToken cancellationToken = default);
}

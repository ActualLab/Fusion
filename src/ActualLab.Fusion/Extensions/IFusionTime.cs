namespace ActualLab.Fusion.Extensions;

/// <summary>
/// A compute service that provides auto-invalidating time-related computed values.
/// </summary>
public interface IFusionTime : IComputeService
{
    [ComputeMethod]
    public Task<Moment> Now();
    [ComputeMethod]
    public Task<Moment> Now(TimeSpan updatePeriod);
    [ComputeMethod]
    public Task<string> GetMomentsAgo(Moment moment);
}

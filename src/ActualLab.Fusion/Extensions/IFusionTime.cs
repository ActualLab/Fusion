namespace ActualLab.Fusion.Extensions;

public interface IFusionTime : IComputeService
{
    [ComputeMethod]
    public Task<Moment> Now();
    [ComputeMethod]
    public Task<Moment> Now(TimeSpan updatePeriod);
    [ComputeMethod]
    public Task<string> GetMomentsAgo(Moment moment);
}

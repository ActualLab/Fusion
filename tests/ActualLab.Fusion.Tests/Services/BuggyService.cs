using ActualLab.Interception;

namespace ActualLab.Fusion.Tests.Services;

public interface IBuggyService : IComputeService, IRequiresFullProxy
{
    public void Test();
}

public interface IBuggyServiceClient : IBuggyService;

public class BuggyService : IBuggyService
{
    public virtual void Test() { }
}

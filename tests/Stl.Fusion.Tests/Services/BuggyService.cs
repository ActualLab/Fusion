using ActualLab.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.Services;

public interface IBuggyService
{
    void Test();
}

public interface IBuggyServiceClient : IBuggyService, IRpcService, IRequiresFullProxy
{ }

public class BuggyService : IBuggyService, IComputeService
{
    public void Test() { }
}

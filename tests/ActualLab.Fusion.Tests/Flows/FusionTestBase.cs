using ActualLab.Flows;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Tests.Model;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests.Flows;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public abstract class FlowTestBase(ITestOutputHelper @out) : FusionTestBase(@out)
{
    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureServices(services, isClient);
        var flows = services.AddFlows();

        if (!isClient) {
            services.AddDbContextServices<TestDbContext>(db => {
                db.AddFlows();
            });
        }
    }
}

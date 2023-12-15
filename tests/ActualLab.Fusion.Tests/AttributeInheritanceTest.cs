using ActualLab.Fusion.Tests.Services;

namespace ActualLab.Fusion.Tests;

public class AttributeInheritanceTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task BasicTest()
    {
        var services = CreateServicesWithComputeService<AttributeTestService, AttributeTestServiceImpl>();
        var s = services.GetRequiredService<AttributeTestService>();
        var c = await Computed.Capture(() => s.PublicMethod());
        c.Value.Should().BeTrue();
    }
}

using ActualLab.RestEase;

namespace ActualLab.Tests.RestEase;

// Checks different request patterns with RestEase
public class RestEaseTest(ITestOutputHelper @out) : RpcTestBase(@out)
{
    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureServices(services, isClient);
        if (isClient) {
            var restEase = services.AddRestEase();
            restEase.AddClient<IRestEaseClient>();
        }
    }

    [Fact]
    public async Task GetFromQueryImplicit()
    {
        await ResetClientServices();
        await using var serving = await WebHost.Serve();
        var service = ClientServices.GetRequiredService<IRestEaseClient>();

        (await service.GetFromQueryImplicit("abcD")).Should().Be("abcD");
    }

    [Fact]
    public async Task GetFromQuery()
    {
        await ResetClientServices();
        await using var serving = await WebHost.Serve();
        var service = ClientServices.GetRequiredService<IRestEaseClient>();

        (await service.GetFromQuery("abcD")).Should().Be("abcD");
    }

    [Fact]
    public async Task GetJsonString()
    {
        await ResetClientServices();
        await using var serving = await WebHost.Serve();
        var service = ClientServices.GetRequiredService<IRestEaseClient>();

        (await service.GetJsonString("abcD")).Value.Should().Be("abcD");
    }

    [Fact]
    public async Task GetFromPath()
    {
        await ResetClientServices();
        await using var serving = await WebHost.Serve();
        var service = ClientServices.GetRequiredService<IRestEaseClient>();

        (await service.GetFromPath("abcD")).Should().Be("abcD");
    }

    [Fact]
    public async Task PostFromQueryImplicit()
    {
        await ResetClientServices();
        await using var serving = await WebHost.Serve();
        var service = ClientServices.GetRequiredService<IRestEaseClient>();

        var jsonString = await service.PostFromQueryImplicit("abcD");
        jsonString.Value.Should().Be("abcD");
    }

    [Fact]
    public async Task PostFromQuery()
    {
        await ResetClientServices();
        await using var serving = await WebHost.Serve();
        var service = ClientServices.GetRequiredService<IRestEaseClient>();

        var jsonString = await service.PostFromQuery("abcD");
        jsonString.Value.Should().Be("abcD");
    }

#if NETCOREAPP
    [Fact]
    public async Task GetFromQueryComplex()
    {
        await ResetClientServices();
        await using var serving = await WebHost.Serve();
        var service = ClientServices.GetRequiredService<IRestEaseClient>();

        var model = new QueryParamModel { Name = "alex", Description = "mercer" };
        var result = await service.GetFromQueryComplex(model);
        result.Name.Should().Be(model.Name);
        result.Description.Should().Be(model.Description);
    }
#endif

    [Fact]
    public async Task PostFromPath()
    {
        await ResetClientServices();
        await using var serving = await WebHost.Serve();
        var service = ClientServices.GetRequiredService<IRestEaseClient>();

        var jsonString = await service.PostFromPath("abcD");
        jsonString.Value.Should().Be("abcD");
    }

    [Fact]
    public async Task PostWithBody()
    {
        await ResetClientServices();
        await using var serving = await WebHost.Serve();
        var service = ClientServices.GetRequiredService<IRestEaseClient>();

        var jsonString = await service.PostWithBody(new StringWrapper("abcD"));
        jsonString.Value.Should().Be("abcD");
    }

    [Fact]
    public async Task ConcatQueryAndPath()
    {
        await ResetClientServices();
        await using var serving = await WebHost.Serve();
        var service = ClientServices.GetRequiredService<IRestEaseClient>();

        var jsonString = await service.ConcatQueryAndPath("ab", "cD");
        jsonString.Value.Should().Be("abcD");
    }

    [Fact]
    public async Task ConcatPathAndBody()
    {
        await ResetClientServices();
        await using var serving = await WebHost.Serve();
        var service = ClientServices.GetRequiredService<IRestEaseClient>();

        var jsonString = await service.ConcatPathAndBody("ab", "cD");
        jsonString.Value.Should().Be("abcD");
    }
}

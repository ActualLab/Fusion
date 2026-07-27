namespace ActualLab.Fusion.Tests;

public class ComputedCategoryTest
{
    [Fact]
    public async Task PlainComputeMethodIsNotPrefixed()
    {
        await using var services = CreateServices();
        var service = services.GetRequiredService<ICategoryService>();

        var computed = await Computed.Capture(() => service.GetPlain(1, default));

        computed.Input.Category.Should().NotStartWith("~");
        computed.Input.Category.Should().EndWith("." + nameof(ICategoryService.GetPlain));
    }

    [Fact]
    public async Task ConsolidationSourceAndTargetHaveDistinctCategories()
    {
        await using var services = CreateServices();
        var service = services.GetRequiredService<ICategoryService>();

        var target = (ConsolidatingComputed<int>)await Computed.Capture(() => service.GetConsolidated(1, default));
        var targetCategory = target.Input.Category;
        var sourceCategory = target.Source.Input.Category;

        // The target is what dependents see, so it keeps the plain name
        targetCategory.Should().NotStartWith("~");
        targetCategory.Should().EndWith("." + nameof(ICategoryService.GetConsolidated));
        sourceCategory.Should().Be("~" + targetCategory);
        sourceCategory.Should().NotBe(targetCategory); // The point of the prefix: no aggregation under one key
    }

    [Fact]
    public async Task RegisterAndUnregisterEventsBalance()
    {
        await using var services = CreateServices();
        var service = services.GetRequiredService<ICategoryService>();

        // Produce one computed up front to learn the category, then drop it before counting
        var probe = await Computed.Capture(() => service.GetPlain(0, default));
        var category = probe.Input.Category;
        probe.Invalidate();

        var registered = 0;
        var unregistered = 0;
        var computeds = new List<Computed>(); // Strong refs: a GC'd consistent computed would never unregister
        ComputedRegistry.OnRegister += OnRegister;
        ComputedRegistry.OnUnregister += OnUnregister;
        try {
            const int count = 16;
            for (var i = 1; i <= count; i++)
                computeds.Add(await Computed.Capture(() => service.GetPlain(i, default)));
            Volatile.Read(ref registered).Should().Be(count);
            Volatile.Read(ref unregistered).Should().Be(0);

            foreach (var computed in computeds)
                computed.Invalidate();
            Volatile.Read(ref unregistered).Should().Be(count);
            Volatile.Read(ref registered).Should().Be(count);
        }
        finally {
            ComputedRegistry.OnRegister -= OnRegister;
            ComputedRegistry.OnUnregister -= OnUnregister;
        }
        return;

        void OnRegister(Computed c) {
            if (string.Equals(c.Input.Category, category, StringComparison.Ordinal))
                Interlocked.Increment(ref registered);
        }

        void OnUnregister(Computed c) {
            if (string.Equals(c.Input.Category, category, StringComparison.Ordinal))
                Interlocked.Increment(ref unregistered);
        }
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddFusion().AddComputeService<ICategoryService, CategoryService>();
        return services.BuildServiceProvider();
    }

    public interface ICategoryService : IComputeService
    {
        [ComputeMethod]
        Task<int> GetPlain(int key, CancellationToken cancellationToken);
        [ComputeMethod(ConsolidationDelay = 0)]
        Task<int> GetConsolidated(int key, CancellationToken cancellationToken);
    }

    public class CategoryService : ICategoryService
    {
        public virtual Task<int> GetPlain(int key, CancellationToken cancellationToken)
            => Task.FromResult(key);

        public virtual Task<int> GetConsolidated(int key, CancellationToken cancellationToken)
            => Task.FromResult(key);
    }
}

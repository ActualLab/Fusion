namespace ActualLab.CommandR.Configuration;

/// <summary>
/// A registry of all registered <see cref="CommandHandler"/> instances,
/// resolved from the DI container at construction time.
/// </summary>
public sealed class CommandHandlerRegistry(IServiceProvider services)
{
    public IReadOnlyList<CommandHandler> Handlers { get; } =
        services.GetRequiredService<HashSet<CommandHandler>>().ToArray();
}

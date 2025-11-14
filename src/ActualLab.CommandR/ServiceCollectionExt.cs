namespace ActualLab.CommandR;

public static class ServiceCollectionExt
{
    extension(IServiceCollection services)
    {
        public CommanderBuilder AddCommander()
            => new(services, null);

        public IServiceCollection AddCommander(Action<CommanderBuilder> configure)
            => new CommanderBuilder(services, configure).Services;
    }
}

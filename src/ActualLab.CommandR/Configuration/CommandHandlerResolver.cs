using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Internal;

namespace ActualLab.CommandR.Configuration;

public class CommandHandlerResolver
{
    public record Options
    {
        public LogLevel HandlerLogLevel { get; init; } = LogLevel.Debug;
    }

    protected ILogger Log { get; init; }
    protected CommandHandlerRegistry Registry { get; }

    protected Options Settings { get; }
    protected Func<CommandHandler, Type, bool> Filter { get; }
    protected ConcurrentDictionary<Type, CommandHandlerSet> Cache { get; } = new();

    public CommandHandlerResolver(Options settings, IServiceProvider services)
    {
        Settings = settings;
        Log = services.LogFor(GetType());

        Registry = services.GetRequiredService<CommandHandlerRegistry>();
        var filters = services.GetRequiredService<IEnumerable<CommandHandlerFilter>>().ToArray();
        Filter = (commandHandler, type) => filters.All(f => f.IsCommandHandlerUsed(commandHandler, type));
    }

    public virtual CommandHandlerSet GetCommandHandlers(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type commandType)
        => Cache.GetOrAdd(commandType, static (commandType1, self) => {
            if (!typeof(ICommand).IsAssignableFrom(commandType1))
                throw new ArgumentOutOfRangeException(nameof(commandType1));

#pragma warning disable IL2067
            var baseTypes = commandType1.GetAllBaseTypes(true, true)
#pragma warning restore IL2067
                .Select((type, index) => (Type: type, Index: index))
                .ToArray();
            var handlers = (
                from typeEntry in baseTypes
                from handler in self.Registry.Handlers
                where handler.CommandType == typeEntry.Type && self.Filter.Invoke(handler, commandType1)
                orderby handler.Priority descending, typeEntry.Index descending
                select handler
            ).Distinct().ToList();

            var logLevel = self.Settings.HandlerLogLevel;
            if (self.Log.IfEnabled(logLevel) is { } log) {
                var sHandlers = handlers.ToDelimitedString($"{Environment.NewLine}- ");
                var message = $"Command handlers for {{CommandType}}:{Environment.NewLine}{{Handlers}}";
                // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                log.Log(logLevel, message, commandType1.GetName(), sHandlers);
            }
            var nonFilterHandlers = handlers.Where(h => !h.IsFilter);

            if (typeof(IEventCommand).IsAssignableFrom(commandType1)) {
                // IEventCommand
                var handlerChains = (
                    from nonFilterHandler in nonFilterHandlers
                    let handlerSubset = handlers.Where(h => h.IsFilter || h == nonFilterHandler).ToImmutableArray()
                    select KeyValuePair.Create(nonFilterHandler.Id, handlerSubset)
                ).ToImmutableDictionary();
                return new CommandHandlerSet(commandType1, handlerChains);
            }

            // Regular ICommand
            if (nonFilterHandlers.Count() > 1) {
                var e = Errors.MultipleNonFilterHandlers(commandType1);
                self.Log.LogCritical(e,
                    "Multiple non-filter handlers are found for '{CommandType}': {Handlers}",
                    commandType1, handlers.ToDelimitedString());
                throw e;
            }
            return new CommandHandlerSet(commandType1, handlers.ToImmutableArray());
        }, this);
}

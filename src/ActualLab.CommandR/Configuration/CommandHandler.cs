namespace ActualLab.CommandR.Configuration;

public interface ICommandHandler
{
    public string Id { get; }
    public bool IsFilter { get; }
    public double Priority { get; }

    public Type GetHandlerServiceType();
    public object GetHandlerService(ICommand command, CommandContext context);
    public Task Invoke(ICommand command, CommandContext context, CancellationToken cancellationToken);
}

public abstract record CommandHandler(
    string Id,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type CommandType,
    bool IsFilter = false,
    double Priority = 0
    ) : ICommandHandler
{
    public abstract Type GetHandlerServiceType();
    public abstract object GetHandlerService(ICommand command, CommandContext context);

    public abstract Task Invoke(
        ICommand command, CommandContext context,
        CancellationToken cancellationToken);

    public override string ToString()
        => $"{Id}[Priority = {Priority}{(IsFilter ? ", IsFilter = true" : "")}]";

    // This record relies on reference-based equality
    public virtual bool Equals(CommandHandler? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}

public abstract record CommandHandler<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TCommand>
    (string Id, bool IsFilter = false, double Priority = 0)
    : CommandHandler(Id, typeof(TCommand), IsFilter, Priority)
    where TCommand : class, ICommand
{
    public override string ToString() => base.ToString();

    // This record relies on reference-based equality
    public virtual bool Equals(CommandHandler<TCommand>? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}

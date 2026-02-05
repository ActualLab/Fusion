namespace ActualLab.CommandR.Configuration;

#pragma warning disable CA1813 // Consider making sealed

/// <summary>
/// Marks a method as a command filter handler (a handler with
/// <see cref="CommandHandlerAttribute.IsFilter"/> set to <c>true</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class CommandFilterAttribute : CommandHandlerAttribute
{
    public CommandFilterAttribute()
        => IsFilter = true;
    public CommandFilterAttribute(int priority) : base(priority)
        => IsFilter = true;
}

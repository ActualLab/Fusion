namespace ActualLab.CommandR.Configuration;

#pragma warning disable CA1813 // Consider making sealed

/// <summary>
/// Marks a method as a command handler, optionally specifying priority and filter mode.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class CommandHandlerAttribute : Attribute
{
    public bool IsFilter { get; set; } = false;
#pragma warning disable CA1019
    public double Priority { get; set; }
#pragma warning restore CA1019

    public CommandHandlerAttribute() { }
    public CommandHandlerAttribute(int priority) { Priority = priority; }
}

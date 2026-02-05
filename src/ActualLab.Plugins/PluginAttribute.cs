namespace ActualLab.Plugins;

#pragma warning disable CA1813 // Consider making sealed

/// <summary>
/// Marks a class as a discoverable plugin for the <see cref="FileSystemPluginFinder"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class PluginAttribute : Attribute
{
    public bool IsEnabled { get; set; } = true;
}

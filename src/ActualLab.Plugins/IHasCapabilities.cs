namespace ActualLab.Plugins;

// Implement it in your plugin to support capabilities extraction
// and filtering based on capabilities

/// <summary>
/// Implement in a plugin to expose capability metadata for filtering and discovery.
/// </summary>
public interface IHasCapabilities
{
    public PropertyBag Capabilities { get; }
}

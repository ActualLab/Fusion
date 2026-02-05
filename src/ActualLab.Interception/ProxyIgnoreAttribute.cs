namespace ActualLab.Interception;

#pragma warning disable CA1813 // Consider making sealed

/// <summary>
/// Marks a method to be ignored by the proxy interceptor.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ProxyIgnoreAttribute : Attribute;

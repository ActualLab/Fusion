namespace ActualLab.DependencyInjection;

/// <summary>
/// Marks a constructor as the preferred constructor for DI service activation.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor)]
public sealed class ServiceConstructorAttribute : ActivatorUtilitiesConstructorAttribute;

using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Fusion.Interception;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class ComputedOptionsProvider
{
    public virtual ComputedOptions? GetComputedOptions(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        MethodInfo method)
        => ComputedOptions.Get(type, method);
}

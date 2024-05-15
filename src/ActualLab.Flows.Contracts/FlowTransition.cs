
namespace ActualLab.Flows;

[StructLayout(LayoutKind.Auto)]
public readonly record struct FlowTransition(
    Symbol Step,
    bool MustSave = true,
    bool MustWaitForEvent = true);

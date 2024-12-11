using System.Diagnostics.CodeAnalysis;
using ActualLab.Trimming;

namespace ActualLab.Interception.Trimming;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "CodeKeepers are used only to retain the code")]
public class MethodDefCodeKeeper : CodeKeeper
{
    public virtual void KeepCodeForResult<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>()
    {
        if (AlwaysTrue)
            return;

        Keep<MethodDef>().KeepCodeForResult<TResult, TUnwrapped>();
    }
}

using System.Diagnostics.CodeAnalysis;
using ActualLab.Trimming;

namespace ActualLab.Interception.Trimming;

public class MethodDefCodeKeeper : CodeKeeper
{
    public virtual void KeepCodeForResult<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>()
    {
        if (AlwaysTrue)
            return;

#pragma warning disable IL2111
        Keep<MethodDef>().KeepCodeForResult<TResult, TUnwrapped>();
#pragma warning restore IL2111
    }
}

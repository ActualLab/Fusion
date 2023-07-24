namespace Stl.Fusion.Internal;

public interface IComputedImpl : IComputed
{
    IComputedImpl[] Used { get; }
    (ComputedInput Input, LTag Version)[] UsedBy { get; }

    void AddUsed(IComputedImpl used);
    bool AddUsedBy(IComputedImpl usedBy); // Should be called only from AddUsed
    void RemoveUsedBy(IComputedImpl usedBy);
    (int OldCount, int NewCount) PruneUsedBy();
    void CopyUsedTo(ref ArrayBuffer<IComputedImpl> buffer);

    void RenewTimeouts(bool isNew);
    void CancelTimeouts();

    bool IsTransientError(Exception error);
}

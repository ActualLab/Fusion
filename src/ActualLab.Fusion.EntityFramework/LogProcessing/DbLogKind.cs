namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public enum DbLogKind
{
    Operations = 0, // Every reader processes each entry - used for operation log / invalidations
    Events, // Just a single reader processes each entry - used for events
}

public static class DbLogKindExt
{
    public static DbHint[] ExclusiveReadBatchQueryHints { get; set; } =  DbHintSet.UpdateSkipLocked;
    public static DbHint[] ExclusiveReadOneQueryHints { get; set; } =  DbHintSet.Update;
    public static DbHint[] CooperativeReadBatchQueryHints { get; set; } = DbHintSet.Empty;
    public static DbHint[] CooperativeReadOneQueryHints { get; set; } = DbHintSet.Empty;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOperationLog(this DbLogKind logKind)
        => logKind == DbLogKind.Operations;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEventLog(this DbLogKind logKind)
        => logKind == DbLogKind.Events;

    public static DbHint[] GetReadBatchQueryHints(this DbLogKind mode)
        => mode.IsEventLog()
            ? ExclusiveReadBatchQueryHints
            : CooperativeReadBatchQueryHints;

    public static DbHint[] GetReadOneQueryHints(this DbLogKind mode)
        => mode.IsEventLog()
            ? ExclusiveReadOneQueryHints
            : CooperativeReadOneQueryHints;
}

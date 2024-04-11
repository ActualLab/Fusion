namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public enum DbLogKind
{
    Operations = 0, // Every reader processes each entry - used for operation log / invalidations
    Events, // Just a single reader processes each entry - used for outbox items
    Timers, // Just a single reader processes each entry - used for outbox items
}

public static class DbLogKindExt
{
    public static DbHint[] UnoProcessorReadBatchQueryHints { get; set; } =  DbHintSet.UpdateSkipLocked;
    public static DbHint[] UnoProcessorReadOneQueryHints { get; set; } =  DbHintSet.Update;
    public static DbHint[] CoProcessorReadBatchQueryHints { get; set; } = DbHintSet.Empty;
    public static DbHint[] CoProcessorReadOneQueryHints { get; set; } = DbHintSet.Empty;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOperationLog(this DbLogKind logKind)
        => logKind == DbLogKind.Operations;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEventLog(this DbLogKind logKind)
        => logKind == DbLogKind.Events;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTimerLog(this DbLogKind logKind)
        => logKind == DbLogKind.Timers;

    // All hosts process every log entry
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCoProcessed(this DbLogKind logKind)
        => logKind == DbLogKind.Operations;

    // An entry must be processed by a single host only
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUnoProcessed(this DbLogKind logKind)
        => logKind != DbLogKind.Operations;

    public static DbHint[] GetReadBatchQueryHints(this DbLogKind mode)
        => mode.IsUnoProcessed()
            ? UnoProcessorReadBatchQueryHints
            : CoProcessorReadBatchQueryHints;

    public static DbHint[] GetReadOneQueryHints(this DbLogKind mode)
        => mode.IsUnoProcessed()
            ? UnoProcessorReadOneQueryHints
            : CoProcessorReadOneQueryHints;
}

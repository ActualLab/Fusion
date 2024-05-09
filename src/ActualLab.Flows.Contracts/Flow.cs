namespace ActualLab.Flows;

public abstract class Flow : IHasId<FlowId>, IHasId<Symbol>, IHasId<string>
{
    Symbol IHasId<Symbol>.Id => Id.Id;
    string IHasId<string>.Id => Id.Value;

    [IgnoreDataMember, MemoryPackIgnore]
    public FlowId Id { get; private set; }
    [IgnoreDataMember, MemoryPackIgnore]
    public long Version { get; private set; }

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public Symbol ResumeLabel { get; internal set; }

    public void Initialize(FlowId id, long version)
    {
        Id = id;
        Version = version;
    }
}

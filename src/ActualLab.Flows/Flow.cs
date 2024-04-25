namespace ActualLab.Flows;

public abstract class Flow
{
    [DataMember(Order = 0), MemoryPackOrder(0)]
    public Symbol NextLabel { get; private set; }
}

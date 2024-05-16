namespace ActualLab.Flows.Infrastructure;

public interface IWorkerFlow
{
    FlowHost Host { get; }
    FlowWorker Worker { get; }
    FlowEventSource Event { get; }
}

namespace ActualLab.Fusion.Client.Interception;

public record RemoteComputeCallOptions
{
    public static RemoteComputeCallOptions Default { get; set; } = new();

    public bool ValidateRpcCallOrigin { get; set; } = true;
}

namespace ActualLab.Fusion.Client.Interception;

public record RpcComputeCallOptions
{
    public static RpcComputeCallOptions Default { get; set; } = new();

    public bool ValidateRpcCallOrigin { get; set; } = true;
}

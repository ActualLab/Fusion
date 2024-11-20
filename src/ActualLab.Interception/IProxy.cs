namespace ActualLab.Interception;

public interface IProxy : IRequiresAsyncProxy
{
    public Interceptor Interceptor { get; set; }
}

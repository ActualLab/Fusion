namespace ActualLab.Interception;

/// <summary>
/// Defines a callback invoked when a proxy is fully initialized.
/// </summary>
public interface INotifyInitialized
{
    public void Initialized();
}

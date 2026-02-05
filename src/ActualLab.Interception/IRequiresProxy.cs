namespace ActualLab.Interception;

/// <summary>
/// A tagging interface indicating that the implementing type requires an async proxy.
/// </summary>
public interface IRequiresAsyncProxy;

/// <summary>
/// A tagging interface indicating that the implementing type requires a full proxy
/// supporting both sync and async method interception.
/// </summary>
public interface IRequiresFullProxy : IRequiresAsyncProxy;

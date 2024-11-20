using ActualLab.Plugins.Metadata;

namespace ActualLab.Plugins.Internal;

public interface IPluginInstanceHandle : IDisposable
{
    public object Instance { get; }
}

public interface IPluginInstanceHandle<out TPluginImpl> : IPluginInstanceHandle
    where TPluginImpl : notnull
{
    public new TPluginImpl Instance { get; }
}

public class PluginInstanceHandle<TPluginImpl> : IPluginInstanceHandle<TPluginImpl>
    where TPluginImpl : notnull
{
#pragma warning disable IL2091
    private Lazy<TPluginImpl>? _lazyInstance;
#pragma warning restore IL2091
    public TPluginImpl Instance =>
        (_lazyInstance ?? throw new ObjectDisposedException(GetType().Name)).Value;
    // ReSharper disable once HeapView.BoxingAllocation
    object IPluginInstanceHandle.Instance => Instance;

    public PluginInstanceHandle(PluginSetInfo plugins,
        IPluginFactory pluginFactory, IEnumerable<IPluginFilter> pluginFilters)
    {
        var pluginType = typeof(TPluginImpl);
        var pluginInfo = plugins.InfoByType.GetValueOrDefault(pluginType);
        if (pluginInfo == null)
            throw Errors.UnknownPluginImplementationType(pluginType);
        if (pluginFilters.Any(f => !f.IsEnabled(pluginInfo)))
            throw Errors.PluginDisabled(pluginType);
#pragma warning disable IL2091
        _lazyInstance = new Lazy<TPluginImpl>(
            () => (TPluginImpl) pluginFactory.Create(pluginType)!);
#pragma warning restore IL2091
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        var lazyInstance = _lazyInstance;
        _lazyInstance = null;
        var disposable = lazyInstance == null ? null : lazyInstance.Value as IDisposable;
        disposable?.Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

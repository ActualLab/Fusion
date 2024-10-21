using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception.Internal;
using ActualLab.OS;
using ActualLab.Trimming;

namespace ActualLab.Interception;

#pragma warning disable CS0420 // A reference to a volatile field will not be treated as volatile

public abstract class Interceptor : IHasServices
{
    public abstract record Options
    {
        public static class Defaults
        {
            public static int HandlerCacheConcurrencyLevel { get; set; } = HardwareInfo.ProcessorCountPo2.Clamp(1, 8);
            public static int HandlerCacheCapacity { get; set; } = 17;
            public static LogLevel LogLevel { get; set; } = LogLevel.Debug;
            public static LogLevel ValidationLogLevel { get; set; } = LogLevel.Debug;
            public static bool IsValidationEnabled { get; set; } = true;
        }

        public int HandlerCacheConcurrencyLevel { get; init; } = Defaults.HandlerCacheConcurrencyLevel;
        public int HandlerCacheCapacity { get; init; } = Defaults.HandlerCacheCapacity;
        public LogLevel LogLevel { get; init; } = Defaults.LogLevel;
        public LogLevel ValidationLogLevel { get; init; } = Defaults.ValidationLogLevel;
        public bool IsValidationEnabled { get; init; } = Defaults.IsValidationEnabled;
    }

    private static readonly MethodInfo CreateTypedHandlerMethod = typeof(Interceptor)
        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
        .Single(m => string.Equals(m.Name, nameof(CreateHandler), StringComparison.Ordinal));

    private readonly Func<MethodInfo, Invocation, Func<Invocation, object?>?> _createHandlerUntyped;
    private readonly Func<MethodInfo, Type, MethodDef?> _createMethodDef;
    private readonly ConcurrentDictionary<Type, Unit> _validateTypeCache;
    private readonly ConcurrentDictionary<MethodInfo, MethodDef?> _methodDefCache;
    private readonly ConcurrentDictionary<MethodInfo, Func<Invocation, object?>?> _handlerCache;

    protected readonly ILogger Log;
    protected readonly ILogger? DefaultLog;
    protected readonly ILogger? ValidationLog;
    protected readonly LogLevel LogLevel;
    protected readonly LogLevel ValidationLogLevel;

    public IServiceProvider Services { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }
    public bool IsValidationEnabled { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }
    public bool MustInterceptAsyncCalls { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; init; } = true;
    public bool MustInterceptSyncCalls { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; init; } = false;
    public bool MustValidateProxyType { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; init; } = true;

    protected Interceptor(Options settings, IServiceProvider services)
    {
        Services = services;
        IsValidationEnabled = settings.IsValidationEnabled;

        LogLevel = settings.LogLevel;
        ValidationLogLevel = settings.ValidationLogLevel;
        Log = Services.LogFor(GetType());
        DefaultLog = Log.IfEnabled(settings.LogLevel);
        ValidationLog = Log.IfEnabled(settings.ValidationLogLevel);

        _createHandlerUntyped = CreateHandlerUntyped;
        _createMethodDef = CreateMethodDef;
        _validateTypeCache = new ConcurrentDictionary<Type, Unit>(
            settings.HandlerCacheConcurrencyLevel, settings.HandlerCacheCapacity);
        _methodDefCache = new ConcurrentDictionary<MethodInfo, MethodDef?>(
            settings.HandlerCacheConcurrencyLevel, settings.HandlerCacheCapacity);
        _handlerCache = new ConcurrentDictionary<MethodInfo, Func<Invocation, object?>?>(
            settings.HandlerCacheConcurrencyLevel, settings.HandlerCacheCapacity);
    }

    public void BindTo(IRequiresAsyncProxy proxy, object? proxyTarget = null, bool initialize = true)
    {
        if (MustInterceptSyncCalls && proxy is not IRequiresFullProxy && MustValidateProxyType)
            throw Errors.InvalidProxyType(proxy.GetType(), typeof(IRequiresFullProxy));

        proxy.RequireProxy<IProxy>().Interceptor = this;
        if (proxyTarget != null)
            proxy.RequireProxy<InterfaceProxy>().ProxyTarget = proxyTarget;
        // ReSharper disable once SuspiciousTypeConversion.Global
        if (initialize && proxy is INotifyInitialized notifyInitialized)
            notifyInitialized.Initialized();
    }

    /// <summary>
    /// Invoked for intercepted calls returning <see cref="Void"/> type.
    /// </summary>
    /// <param name="invocation">Invocation descriptor.</param>
    public void Intercept(Invocation invocation)
    {
        var handler = SelectHandler(invocation);
        if (handler != null)
            handler.Invoke(invocation);
        else
            invocation.InvokeIntercepted();
    }

    /// <summary>
    /// Invoked for intercepted calls returning non-<see cref="Void"/> type.
    /// </summary>
    /// <param name="invocation"></param>
    /// <typeparam name="TResult">The type of method call result.</typeparam>
    /// <returns>Method call result.</returns>
    public TResult Intercept<TResult>(Invocation invocation)
    {
        var handler = SelectHandler(invocation);
        return handler != null
            ? (TResult)handler.Invoke(invocation)!
            : invocation.InvokeIntercepted<TResult>();
    }

    public virtual Func<Invocation, object?>? SelectHandler(in Invocation invocation)
        => GetHandler(invocation);

    public Func<Invocation, object?>? GetHandler(in Invocation invocation)
    {
        if (_handlerCache.TryGetValue(invocation.Method, out var handler))
            return handler;

        return _handlerCache.GetOrAdd(invocation.Method, _createHandlerUntyped, invocation);
    }

    public virtual MethodDef? GetMethodDef(MethodInfo method, Type proxyType)
        => _methodDefCache.GetOrAdd(method, _createMethodDef, proxyType);

    public void ValidateType(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
    {
        if (!IsValidationEnabled)
            return;

        _validateTypeCache.GetOrAdd(type, static (type1, self) => {
            self.ValidationLog?.Log(self.ValidationLogLevel, "Validating: '{Type}'", type1);
            try {
#pragma warning disable IL2067
                self.ValidateTypeInternal(type1);
#pragma warning restore IL2067
            }
            catch (Exception e) {
                self.Log.LogCritical(e, "Validation of '{Type}' failed", type1);
                throw;
            }
            return default;
        }, this);
    }

    // Protected methods

    protected internal abstract Func<Invocation, object?>? CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        Invocation initialInvocation, MethodDef methodDef);

    protected virtual MethodDef? CreateMethodDef(MethodInfo method, Type proxyType)
    {
        var methodDef = new MethodDef(proxyType, method);
        var mustIntercept = methodDef.IsAsyncMethod
            ? MustInterceptAsyncCalls
            : MustInterceptSyncCalls;
        return mustIntercept ? methodDef : null;
    }

    protected virtual void ValidateTypeInternal(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        Type type)
    { }

    protected internal virtual void KeepCodeForResult<TResult, TUnwrapped>()
    {
        if (CodeKeeper.AlwaysFalse)
            CreateHandler<TUnwrapped>(default, null!);
    }

    // Private methods

    private Func<Invocation, object?>? CreateHandlerUntyped(MethodInfo method, Invocation initialInvocation)
    {
        var methodDef = GetMethodDef(method, initialInvocation.Proxy.GetType());
        if (methodDef == null)
            return null;

        return (Func<Invocation, object?>?)CreateTypedHandlerMethod
            .MakeGenericMethod(methodDef.UnwrappedReturnType)
            .Invoke(this, [initialInvocation, methodDef])!;
    }
}

using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception.Internal;
using ActualLab.OS;
using ActualLab.Trimming;

namespace ActualLab.Interception;

#pragma warning disable CS0420 // A reference to a volatile field will not be treated as volatile

[UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume proxy-related code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume proxy-related code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "We assume proxy-related code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume proxy-related code is preserved")]
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
        .Single(m => string.Equals(m.Name, nameof(CreateTypedHandler), StringComparison.Ordinal));

    private readonly Func<MethodInfo, Invocation, Func<Invocation, object?>?> _createHandler;
    private readonly Func<MethodInfo, Type, MethodDef?> _createMethodDef;
    private readonly ConcurrentDictionary<Type, Unit> _validateTypeCache;
    private readonly ConcurrentDictionary<MethodInfo, MethodDef?> _methodDefCache;
    private readonly ConcurrentDictionary<MethodInfo, Func<Invocation, object?>?> _handlerCache;

    protected readonly ILogger Log;
    protected readonly ILogger? DefaultLog;
    protected readonly ILogger? ValidationLog;
    protected readonly LogLevel LogLevel;
    protected readonly LogLevel ValidationLogLevel;
    protected bool UsesUntypedHandlers { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; init; }

    public IServiceProvider Services { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }
    public bool IsValidationEnabled { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }
    public bool MustInterceptAsyncCalls { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; init; } = true;
    public bool MustInterceptSyncCalls { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; init; } = false;
    public bool MustValidateProxyType { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; init; } = true;

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Interceptor))]
    static Interceptor()
    { }

    protected Interceptor(Options settings, IServiceProvider services)
    {
        Services = services;
        IsValidationEnabled = settings.IsValidationEnabled;

        LogLevel = settings.LogLevel;
        ValidationLogLevel = settings.ValidationLogLevel;
        Log = Services.LogFor(GetType());
        DefaultLog = Log.IfEnabled(settings.LogLevel);
        ValidationLog = Log.IfEnabled(settings.ValidationLogLevel);

        _createHandler = CreateHandler;
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

    /// <summary>
    /// Invoked for intercepted calls returning any type.
    /// </summary>
    /// <param name="invocation"></param>
    /// <returns>Method call result.</returns>
#if NET5_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public object? InterceptUntyped(Invocation invocation)
    {
        var handler = SelectHandler(invocation);
        return handler != null
            ? handler.Invoke(invocation)!
            : invocation.InvokeInterceptedUntyped();
    }

    public virtual Func<Invocation, object?>? SelectHandler(in Invocation invocation)
        => GetHandler(invocation);

    public Func<Invocation, object?>? GetHandler(in Invocation invocation)
    {
        if (_handlerCache.TryGetValue(invocation.Method, out var handler))
            return handler;

        return _handlerCache.GetOrAdd(invocation.Method, _createHandler, invocation);
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
                self.ValidateTypeInternal(type1);
            }
            catch (Exception e) {
                self.Log.LogCritical(e, "Validation of '{Type}' failed", type1);
                throw;
            }
            return default;
        }, this);
    }

    // Protected methods

    protected internal virtual Func<Invocation, object?>? CreateUntypedHandler(
        Invocation initialInvocation, MethodDef methodDef)
    {
        if (!UsesUntypedHandlers)
            throw ActualLab.Internal.Errors.NotSupported($"{GetType().Name} uses typed handlers.");

        throw ActualLab.Internal.Errors.InternalError($"{GetType().Name} requires this method to be implemented.");
    }

    protected internal virtual Func<Invocation, object?>? CreateTypedHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        Invocation initialInvocation, MethodDef methodDef)
    {
        if (UsesUntypedHandlers)
            throw ActualLab.Internal.Errors.NotSupported($"{GetType().Name} uses untyped handlers.");

        throw ActualLab.Internal.Errors.InternalError($"{GetType().Name} requires this method to be implemented.");
    }

    protected Func<Invocation, object?>? CreateHandler(MethodInfo method, Invocation initialInvocation)
    {
        var methodDef = GetMethodDef(method, initialInvocation.Proxy.GetType());
        if (methodDef == null)
            return null;

        if (UsesUntypedHandlers)
            return CreateUntypedHandler(initialInvocation, methodDef);

        return (Func<Invocation, object?>?)CreateTypedHandlerMethod
            .MakeGenericMethod(methodDef.UnwrappedReturnType)
            .Invoke(this, [initialInvocation, methodDef])!;
    }

    protected virtual MethodDef? CreateMethodDef(MethodInfo method,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyType)
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

    protected internal virtual void KeepCodeForResult<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>()
    {
        if (CodeKeeper.AlwaysFalse)
            CreateTypedHandler<TUnwrapped>(default, null!);
    }
}

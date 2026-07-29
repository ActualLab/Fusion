using System.Collections.ObjectModel;
using ActualLab.Internal;

namespace ActualLab.Rpc;

/// <summary>
/// Holds the set of registered RPC service builders and default service mode.
/// Frozen after <see cref="RpcHub"/> construction to prevent further modification.
/// </summary>
public class RpcConfiguration
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private IDictionary<Type, RpcServiceBuilder> _services = new Dictionary<Type, RpcServiceBuilder>();

    // Volatile on both sides: AssertNotFrozen reads it without the lock, so a stale false
    // silently skips the guard - and the true is what publishes the frozen _services
    public bool IsFrozen {
        get => Volatile.Read(ref field);
        private set => Volatile.Write(ref field, value);
    }

    public RpcServiceMode DefaultServiceMode {
        get;
        set {
            AssertNotFrozen();
            field = value.Or(RpcServiceMode.Server);
        }
    }

    public IDictionary<Type, RpcServiceBuilder> Services {
        get => _services;
        set {
            AssertNotFrozen();
            _services = value;
        }
    }

    public void Freeze()
    {
        if (IsFrozen)
            return;

        lock (_lock) {
            if (IsFrozen) // Double-check locking
                return;

            // IsFrozen is stored last, so no one can observe the frozen flag
            // while Services still returns the mutable dictionary
            Volatile.Write(ref _services, new ReadOnlyDictionary<Type, RpcServiceBuilder>(
                new Dictionary<Type, RpcServiceBuilder>(Services)));
            IsFrozen = true;
        }
    }

    // Protected methods

    protected void AssertNotFrozen()
    {
        if (IsFrozen)
            throw Errors.AlreadyReadOnly<RpcConfiguration>();
    }
}

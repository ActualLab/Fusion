namespace ActualLab.Fusion;

/// <summary>
/// Describes how a command handler declares its invalidations and how far they reach.
/// See <see cref="DeferredInvalidationModeAttribute"/> for how it's declared.
/// </summary>
/// <remarks>
/// Every mode here is a real carrier, and a handler that defers a block resolves to one of them -
/// its own attribute's, its implementation type's, or its service type's. There is no fallback and
/// no "undecided" member: an undeclared mode throws, and a
/// <see cref="DeferredInvalidationContext"/> that hasn't been told a mode yet holds
/// <c>null</c> rather than a member of this enum. The first handler that defers a block is what
/// gives it one.
/// </remarks>
public enum DeferredInvalidationMode
{
    Local = 0,
    Replicated = 1,
    Distributed = 2,
}

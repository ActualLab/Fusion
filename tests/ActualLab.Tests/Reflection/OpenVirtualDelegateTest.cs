using System.Reflection;
using ActualLab.Reflection;

namespace ActualLab.Tests.Reflection;

// Covers the dotnet/runtime#130840 workaround in MemberInfoExt: on affected Apple configurations
// we must not hand out an open-instance delegate over a virtual/interface accessor, and must fall
// back to emitted codegen instead. Neither branch is reachable from the other on a given platform,
// so both are driven explicitly here.
public class OpenVirtualDelegateTest
{
    private interface IHasValue
    {
        int Value { get; set; }
    }

    private class Holder : IHasValue
    {
        public virtual int Value { get; set; }
    }

    private sealed class SealedHolder
    {
        public int Value { get; set; } // non-virtual accessor
    }

    // The decision itself, driven with both flag values - independent of the ambient platform.

    [Theory]
    [InlineData(typeof(IHasValue), false, true)]  // interface accessor, flag off -> shortcut allowed
    [InlineData(typeof(IHasValue), true, false)]  // interface accessor, flag on  -> must emit
    [InlineData(typeof(Holder), false, true)]     // virtual accessor,   flag off -> shortcut allowed
    [InlineData(typeof(Holder), true, false)]     // virtual accessor,   flag on  -> must emit
    [InlineData(typeof(SealedHolder), false, true)]
    [InlineData(typeof(SealedHolder), true, true)] // non-virtual: flag is irrelevant
    public void DecidesPerAccessorVirtuality(Type type, bool mustAvoidOpenVirtual, bool expected)
    {
        var pi = type.GetProperty(nameof(IHasValue.Value))!;
        CanUseAccessorDelegate(pi.GetMethod!, type, type, typeof(int), typeof(int), mustAvoidOpenVirtual)
            .Should().Be(expected);
        CanUseAccessorDelegate(pi.SetMethod!, type, type, typeof(int), typeof(int), mustAvoidOpenVirtual)
            .Should().Be(expected);
    }

#if NET11_0_OR_GREATER
    // End-to-end: set the real flag, clear the caches, and check which delegate actually comes out
    // and that it still works. The "avoided" case is the path an affected iOS build takes.
    // .NET 11 only - MustAvoidOpenVirtualDelegates is a const false on every other target, so there
    // is no second branch to drive there.

    [Fact]
    public void InterfaceAccessorUsesDirectDelegateByDefault()
    {
        WithMustAvoid(false, () => {
            var pi = typeof(IHasValue).GetProperty(nameof(IHasValue.Value))!;
            var getter = pi.GetGetter<IHasValue, int>();
            var setter = pi.GetSetter<IHasValue, int>();

            // The shortcut hands out a delegate bound straight to the accessor.
            getter.Method.Should().BeSameAs(pi.GetMethod);
            setter.Method.Should().BeSameAs(pi.SetMethod);

            var holder = new Holder();
            setter.Invoke(holder, 42);
            holder.Value.Should().Be(42);
            getter.Invoke(holder).Should().Be(42);
        });
    }

    [Fact]
    public void InterfaceAccessorUsesEmittedDelegateWhenOpenVirtualIsAvoided()
    {
        WithMustAvoid(true, () => {
            var pi = typeof(IHasValue).GetProperty(nameof(IHasValue.Value))!;
            var getter = pi.GetGetter<IHasValue, int>();
            var setter = pi.GetSetter<IHasValue, int>();

            // Not the accessor itself any more - emitted codegen stands in for it.
            getter.Method.Should().NotBeSameAs(pi.GetMethod);
            setter.Method.Should().NotBeSameAs(pi.SetMethod);

            // ...and it still dispatches virtually to the concrete implementation.
            var holder = new Holder();
            setter.Invoke(holder, 42);
            holder.Value.Should().Be(42);
            getter.Invoke(holder).Should().Be(42);
        });
    }

    [Fact]
    public void NonVirtualAccessorIsUnaffectedByTheWorkaround()
    {
        WithMustAvoid(true, () => {
            var pi = typeof(SealedHolder).GetProperty(nameof(SealedHolder.Value))!;
            var getter = pi.GetGetter<SealedHolder, int>();

            getter.Method.Should().BeSameAs(pi.GetMethod);

            var holder = new SealedHolder { Value = 7 };
            getter.Invoke(holder).Should().Be(7);
        });
    }

#endif

    // Private methods

    private static bool CanUseAccessorDelegate(
        MethodInfo accessor, Type declaringType, Type sourceType, Type valueType, Type propertyType,
        bool mustAvoidOpenVirtual)
    {
        var m = typeof(MemberInfoExt).GetMethod("CanUseAccessorDelegate", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (bool)m.Invoke(null, [accessor, declaringType, sourceType, valueType, propertyType, mustAvoidOpenVirtual])!;
    }

#if NET11_0_OR_GREATER
    // Sets MustAvoidOpenVirtualDelegates for the duration of the action, clearing the delegate
    // caches on both sides so nothing created under the previous value is reused.
    private static void WithMustAvoid(bool value, Action action)
    {
        var old = MemberInfoExt.MustAvoidOpenVirtualDelegates;
        ClearCaches();
        MemberInfoExt.MustAvoidOpenVirtualDelegates = value;
        try {
            action();
        }
        finally {
            MemberInfoExt.MustAvoidOpenVirtualDelegates = old;
            ClearCaches();
        }
    }

    private static void ClearCaches()
    {
        foreach (var name in new[] { "GetterCache", "SetterCache", "UntypedGetterCache", "UntypedSetterCache" }) {
            var cache = typeof(MemberInfoExt).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            cache.GetType().GetMethod("Clear")!.Invoke(cache, null);
        }
    }
#endif
}

using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Collections;

public static class PropertyBagExt
{
    // ToMutable

    public static MutablePropertyBag ToMutable(this PropertyBag bag)
        => new(bag);

    // ContainsXxx

    public static bool Contains(this PropertyBag bag, string key)
        => bag[key] != null;
    public static bool Contains(this MutablePropertyBag bag, string key)
        => bag[key] != null;

    public static bool Contains(this PropertyBag bag, Type key)
        => bag[key] != null;
    public static bool Contains(this MutablePropertyBag bag, Type key)
        => bag[key] != null;

    public static bool ContainsKeyless<T>(this PropertyBag bag)
        => bag[typeof(T)] != null;
    public static bool ContainsKeyless<T>(this MutablePropertyBag bag)
        => bag[typeof(T)] != null;

    // TryGetXxx

    public static bool TryGet(this PropertyBag bag, string key, [NotNullWhen(true)] out object? value)
    {
        value = bag[key];
        return value != null;
    }

    public static bool TryGet(this MutablePropertyBag bag, string key, [NotNullWhen(true)] out object? value)
    {
        value = bag[key];
        return value != null;
    }

    public static bool TryGet<T>(this PropertyBag bag, string key, [MaybeNullWhen(false)] out T value)
    {
        var objValue = bag[typeof(T)];
        if (objValue == null) {
            value = default!;
            return false;
        }
        value = (T)objValue;
        return true;
    }

    public static bool TryGet<T>(this MutablePropertyBag bag, string key, [MaybeNullWhen(false)] out T value)
    {
        var objValue = bag[typeof(T)];
        if (objValue == null) {
            value = default!;
            return false;
        }
        value = (T)objValue;
        return true;
    }

    public static bool TryGetKeyless<T>(this PropertyBag bag, [MaybeNullWhen(false)] out T value)
    {
        var objValue = bag[typeof(T)];
        if (objValue == null) {
            value = default!;
            return false;
        }
        value = (T)objValue;
        return true;
    }

    public static bool TryGetKeyless<T>(this MutablePropertyBag bag, [MaybeNullWhen(false)] out T value)
    {
        var objValue = bag[typeof(T)];
        if (objValue == null) {
            value = default!;
            return false;
        }
        value = (T)objValue;
        return true;
    }

    // GetXxx

    public static T? Get<T>(this PropertyBag bag, string key)
    {
        var value = bag[key];
        return value != null ? (T)value : default;
    }

    public static T? Get<T>(this MutablePropertyBag bag, string key)
    {
        var value = bag[key];
        return value != null ? (T)value : default;
    }

    public static T Get<T>(this PropertyBag bag, string key, T @default)
    {
        var value = bag[key];
        return value != null ? (T)value : @default;
    }

    public static T Get<T>(this MutablePropertyBag bag, string key, T @default)
    {
        var value = bag[key];
        return value != null ? (T)value : @default;
    }

    public static T? GetKeyless<T>(this PropertyBag bag)
    {
        var value = bag[typeof(T)];
        return value != null ? (T)value : default;
    }

    public static T? GetKeyless<T>(this MutablePropertyBag bag)
    {
        var value = bag[typeof(T)];
        return value != null ? (T)value : default;
    }

    public static T GetKeyless<T>(this PropertyBag bag, T @default)
    {
        var value = bag[typeof(T)];
        return value != null ? (T)value : @default;
    }

    public static T GetKeyless<T>(this MutablePropertyBag bag, T @default)
    {
        var value = bag[typeof(T)];
        return value != null ? (T)value : @default;
    }

    // SetXxx

    public static void Set(this MutablePropertyBag bag, string key, object? value)
        => bag.Update((key, value), static (kv, bag) => bag.Set(kv.key, kv.value));

    public static PropertyBag SetKeyless<T>(this PropertyBag bag, T value)
        => bag.Set(typeof(T).ToIdentifierSymbol(), value);
    public static void SetKeyless<T>(this MutablePropertyBag bag, T value)
        => bag.Set(typeof(T).ToIdentifierSymbol(), value);

#pragma warning disable CS0618 // Type or member is obsolete
    public static PropertyBag SetMany(this PropertyBag bag, PropertyBag items)
        => bag.SetMany(items.RawItems ?? []);
    public static void SetMany(this MutablePropertyBag bag, PropertyBag items)
        => bag.SetMany(items.RawItems ?? []);
#pragma warning restore CS0618 // Type or member is obsolete

    // RemoveXxx

    public static void Remove(this MutablePropertyBag bag, string key)
        => bag.Update(key, static (key, bag) => bag.Remove(key));

    public static PropertyBag Remove(this PropertyBag bag, Type key)
        => bag.Remove(key.ToIdentifierSymbol());
    public static void Remove(this MutablePropertyBag bag, Type key)
        => bag.Remove(key.ToIdentifierSymbol());

    public static PropertyBag RemoveKeyless<T>(this PropertyBag bag)
        => bag.Remove(typeof(T).ToIdentifierSymbol());
    public static void RemoveKeyless<T>(this MutablePropertyBag bag)
        => bag.Remove(typeof(T).ToIdentifierSymbol());

    // Clear

    public static void Clear(this MutablePropertyBag bag)
        => bag.Update(static _ => PropertyBag.Empty);
}

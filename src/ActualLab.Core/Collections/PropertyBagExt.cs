namespace ActualLab.Collections;

/// <summary>
/// Extension methods for <see cref="PropertyBag"/> and <see cref="MutablePropertyBag"/>.
/// </summary>
public static class PropertyBagExt
{
    // ToMutable

    public static MutablePropertyBag ToMutable(this PropertyBag bag)
        => new(bag);

    // (Keyless)Contains

    public static bool Contains(this PropertyBag bag, string key)
        => bag[key] is not null;
    public static bool Contains(this MutablePropertyBag bag, string key)
        => bag[key] is not null;

    public static bool Contains(this PropertyBag bag, Type key)
        => bag[key] is not null;
    public static bool Contains(this MutablePropertyBag bag, Type key)
        => bag[key] is not null;

    public static bool KeylessContains<T>(this PropertyBag bag)
        => bag[typeof(T)] is not null;
    public static bool KeylessContains<T>(this MutablePropertyBag bag)
        => bag[typeof(T)] is not null;

    // (Keyless)TryGet

    public static bool TryGet(this PropertyBag bag, string key, [NotNullWhen(true)] out object? value)
    {
        value = bag[key];
        return value is not null;
    }

    public static bool TryGet(this MutablePropertyBag bag, string key, [NotNullWhen(true)] out object? value)
    {
        value = bag[key];
        return value is not null;
    }

    public static bool TryGet<T>(this PropertyBag bag, string key, [MaybeNullWhen(false)] out T value)
    {
        var objValue = bag[typeof(T)];
        if (objValue is null) {
            value = default!;
            return false;
        }
        value = (T)objValue;
        return true;
    }

    public static bool TryGet<T>(this MutablePropertyBag bag, string key, [MaybeNullWhen(false)] out T value)
    {
        var objValue = bag[typeof(T)];
        if (objValue is null) {
            value = default!;
            return false;
        }
        value = (T)objValue;
        return true;
    }

    public static bool KeylessTryGet<T>(this PropertyBag bag, [MaybeNullWhen(false)] out T value)
    {
        var objValue = bag[typeof(T)];
        if (objValue is null) {
            value = default!;
            return false;
        }
        value = (T)objValue;
        return true;
    }

    public static bool KeylessTryGet<T>(this MutablePropertyBag bag, [MaybeNullWhen(false)] out T value)
    {
        var objValue = bag[typeof(T)];
        if (objValue is null) {
            value = default!;
            return false;
        }
        value = (T)objValue;
        return true;
    }

    // (Keyless)Get

    public static T? Get<T>(this PropertyBag bag, string key)
    {
        var value = bag[key];
        return value is not null ? (T)value : default;
    }

    public static T? Get<T>(this MutablePropertyBag bag, string key)
    {
        var value = bag[key];
        return value is not null ? (T)value : default;
    }

    public static T Get<T>(this PropertyBag bag, string key, T @default)
    {
        var value = bag[key];
        return value is not null ? (T)value : @default;
    }

    public static T Get<T>(this MutablePropertyBag bag, string key, T @default)
    {
        var value = bag[key];
        return value is not null ? (T)value : @default;
    }

    public static T? KeylessGet<T>(this PropertyBag bag)
    {
        var value = bag[typeof(T)];
        return value is not null ? (T)value : default;
    }

    public static T? KeylessGet<T>(this MutablePropertyBag bag)
    {
        var value = bag[typeof(T)];
        return value is not null ? (T)value : default;
    }

    public static T KeylessGet<T>(this PropertyBag bag, T @default)
    {
        var value = bag[typeof(T)];
        return value is not null ? (T)value : @default;
    }

    public static T KeylessGet<T>(this MutablePropertyBag bag, T @default)
    {
        var value = bag[typeof(T)];
        return value is not null ? (T)value : @default;
    }

    // (Keyless)Set & SetMany

    public static void Set(this MutablePropertyBag bag, string key, object? value)
        => bag.Update((key, value), static (kv, bag) => bag.Set(kv.key, kv.value));

#pragma warning disable CS0618 // Type or member is obsolete
    public static PropertyBag SetMany(this PropertyBag bag, PropertyBag items)
        => bag.SetMany(items.RawItems ?? []);
    public static void SetMany(this MutablePropertyBag bag, PropertyBag items)
        => bag.SetMany(items.RawItems ?? []);
#pragma warning restore CS0618 // Type or member is obsolete

    public static PropertyBag KeylessSet<T>(this PropertyBag bag, T value)
        => bag.Set(typeof(T).ToIdentifierSymbol(), value);
    public static void KeylessSet<T>(this MutablePropertyBag bag, T value)
        => bag.Set(typeof(T).ToIdentifierSymbol(), value);

    // (Keyless)Remove

    public static void Remove(this MutablePropertyBag bag, string key)
        => bag.Update(key, static (key, bag) => bag.Remove(key));

    public static PropertyBag Remove(this PropertyBag bag, Type key)
        => bag.Remove(key.ToIdentifierSymbol());
    public static void Remove(this MutablePropertyBag bag, Type key)
        => bag.Remove(key.ToIdentifierSymbol());

    public static PropertyBag KeylessRemove<T>(this PropertyBag bag)
        => bag.Remove(typeof(T).ToIdentifierSymbol());
    public static void KeylessRemove<T>(this MutablePropertyBag bag)
        => bag.Remove(typeof(T).ToIdentifierSymbol());

    // Clear

    public static void Clear(this MutablePropertyBag bag)
        => bag.Update(static _ => PropertyBag.Empty);
}

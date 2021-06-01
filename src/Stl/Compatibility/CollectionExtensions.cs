using System.Diagnostics.CodeAnalysis;

#if NETSTANDARD2_0

namespace System.Collections.Generic
{
    public static class CollectionExtensions
    {
        public static TValue? GetValueOrDefault<TKey, TValue>(
            this IReadOnlyDictionary<TKey, TValue> dictionary,
            TKey key)
        {
            return dictionary.GetValueOrDefault<TKey, TValue>(key, default);
        }

        public static TValue? GetValueOrDefault<TKey, TValue>(
            this IReadOnlyDictionary<TKey, TValue> dictionary,
            TKey key,
            TValue? defaultValue)
        {
            if (dictionary == null)
                throw new ArgumentNullException(nameof(dictionary));
            TValue obj;
            return !dictionary.TryGetValue(key, out obj) ? defaultValue : obj;
        }

        public static bool TryAdd<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary,
            TKey key,
            TValue value)
        {
            if (dictionary == null)
                throw new ArgumentNullException(nameof(dictionary));
            if (dictionary.ContainsKey(key))
                return false;
            dictionary.Add(key, value);
            return true;
        }

        public static bool Remove<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary,
            TKey key,
            [MaybeNullWhen(false)] out TValue value)
        {
            if (dictionary == null)
                throw new ArgumentNullException(nameof(dictionary));
            if (dictionary.TryGetValue(key, out value)) {
                dictionary.Remove(key);
                return true;
            }

            value = default(TValue);
            return false;
        }

        public static bool TryPop<T>(this Stack<T> stack, [MaybeNullWhen(false)] out T value)
        {
            if (stack.Count == 0) {
                value = default;
                return false;
            }
            value = stack.Pop();
            return true;
        }
    }
}

#endif

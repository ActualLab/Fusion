using System;
using System.ComponentModel;
using Newtonsoft.Json;
using Stl.ImmutableModel.Internal;
using Stl.Reflection;
using Stl.Text;

namespace Stl.ImmutableModel 
{
    [JsonConverter(typeof(KeyJsonConverter))]
    [TypeConverter(typeof(KeyTypeConverter))]
    public abstract class Key : IEquatable<Key>
    {
        public static readonly string NullKeyFormat = "";
        public static readonly StringKey DefaultRootKey = new StringKey("@"); 

        protected internal static readonly ListFormat ListFormat = ListFormat.Default;
        protected internal static readonly char TagPrefix = '@';

        protected int HashCode { get; }
        public Key? Continuation { get; }
        public bool IsComposite => !ReferenceEquals(Continuation, null);
        public int Size => (Continuation?.Size ?? 0) + 1;

        protected Key(int ownHashCode, Key? continuation = null)
        {
            HashCode = unchecked(ownHashCode + 347 * continuation?.HashCode ?? 0);
            Continuation = continuation;
        }

        // Format & Parse

        public override string ToString() => this.Format();

        public abstract void FormatTo(ref ListFormatter formatter); 

        public static Key Parse(string source) 
            => KeyParser.Parse(source) ?? throw new NullReferenceException();
        public static Key Parse(in ReadOnlySpan<char> source) 
            => KeyParser.Parse(source) ?? throw new NullReferenceException();

        // Operators

        public static StringKey operator &(Symbol prefix, Key? suffix) => new StringKey(prefix, suffix);
        public static TypeKey operator &(Type prefix, Key? suffix) => new TypeKey(prefix, suffix);

        // Equality

        public override bool Equals(object? other) => throw new NotImplementedException();
        public abstract bool Equals(Key? other);
        public override int GetHashCode() 
            => HashCode;
        public static bool operator ==(Key? left, Key? right) 
            => left?.Equals(right) ?? ReferenceEquals(right, null);
        public static bool operator !=(Key? left, Key? right) 
            => !(left?.Equals(right) ?? ReferenceEquals(right, null));

        // Protected

        protected static string GetTypeTag(Type type)
        {
            var tagName = type.ToMethodName();
            if (tagName.Length > 0)
                tagName = tagName.Substring(0, 1).ToLowerInvariant() + tagName.Substring(1);
            if (tagName.EndsWith("Key"))
                tagName = tagName.Substring(0, tagName.Length - 3);

            return TagPrefix + tagName;
        }
    }
}

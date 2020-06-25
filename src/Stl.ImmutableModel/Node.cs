using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Stl.Collections;
using Stl.Frozen;
using Stl.ImmutableModel.Indexing;
using Stl.ImmutableModel.Reflection;
using Stl.Text;

namespace Stl.ImmutableModel
{
    [JsonObject]
    public abstract class Node: FrozenBase, INode
    {
        internal static NodeTypeDef CreateNodeTypeDef(Type type) => new NodeTypeDef(type);

        [JsonProperty(
            PropertyName = "@Options", 
            DefaultValueHandling = DefaultValueHandling.Ignore)]
        private Dictionary<Symbol, object>? _options;
        private Dictionary<Symbol, object> Options => _options ??= new Dictionary<Symbol, object>();

        protected abstract Key UntypedKey { get; set; }
        [NotMapped]
        public Key Key {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => UntypedKey; 
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => UntypedKey = value;
        }

        public override string ToString() => $"{GetType().Name}({Key})";

        // IFrozen implementation

        public override void Freeze()
        {
            if (IsFrozen) return;
            Key.ThrowIfNull(); 

            // First we freeze child frozen-s
            var buffer = MemoryBuffer<KeyValuePair<ItemKey, IFrozen>>.Lease();
            try {
                this.GetDefinition().GetFrozenItems(this, ref buffer);
                foreach (var (key, frozen) in buffer)
                    frozen.Freeze();
            }
            finally {
                buffer.Release();
            }
            
            // And freeze itself in the end
            base.Freeze();
        }

        public override IFrozen BaseToUnfrozen(bool deep = false)
        {
            var clone = (Node) base.BaseToUnfrozen(deep);
            var nodeTypeDef = clone.GetDefinition();

            if (deep) {
                // Defrost every frozen
                var buffer = MemoryBuffer<KeyValuePair<ItemKey, IFrozen>>.Lease();
                try {
                    nodeTypeDef.GetFrozenItems(clone, ref buffer);
                    foreach (var (key, f) in buffer)
                        nodeTypeDef.SetItem(clone, key, (object?) f.ToUnfrozen(true));
                }
                finally {
                    buffer.Release();
                }
            }
            else {
                // Defrost every collection (for convenience)
                var buffer = MemoryBuffer<KeyValuePair<ItemKey, ICollectionNode>>.Lease();
                try {
                    nodeTypeDef.GetCollectionNodeItems(clone, ref buffer);
                    foreach (var (key, c) in buffer)
                        nodeTypeDef.SetItem(clone, key, (object?) c.ToUnfrozen());
                }
                finally {
                    buffer.Release();
                }
            }

            return clone;
        }

        // IHasOptions implementation

        public IEnumerable<KeyValuePair<Symbol, object>> GetAllOptions() 
            => _options ?? Enumerable.Empty<KeyValuePair<Symbol, object>>();

        public bool HasOption(Symbol key) => _options?.ContainsKey(key) ?? false;
        public object? GetOption(Symbol key) => _options?.GetValueOrDefault(key);
        
        public void SetOption(Symbol key, object? value)
        {
            key.ThrowIfInvalidOptionsKey();
            if (value == null) {
                this.ThrowIfFrozen();
                _options?.Remove(key);
            }
            else {
                Options[key] = PrepareOptionValue(key, value);
            }
        }

        // IHasChangeHistory

        (object? BaseState, object? CurrentState, IEnumerable<(Key Key, DictionaryEntryChangeType ChangeType, object? Value)> Changes) 
            IHasChangeHistory.GetChangeHistory() 
            => GetChangeHistoryUntyped();
        protected virtual (object? BaseState, object? CurrentState, IEnumerable<(Key Key, DictionaryEntryChangeType ChangeType, object? Value)> Changes) GetChangeHistoryUntyped()
            => (null, null, Enumerable.Empty<(Key Key, DictionaryEntryChangeType ChangeType, object? Value)>());

        void IHasChangeHistory.DiscardChangeHistory() => DiscardChangeHistory();
        protected virtual void DiscardChangeHistory() {}

        // Protected & private members

        protected T PreparePropertyValue<T>(Symbol propertyName, T value)
        {
            this.ThrowIfFrozen();
            if (value is INode node && node.Key.IsNull()) {
                // We automatically provide keys for INode properties (or collection items)
                // by extending the owner's key with property name suffix 
                node.Key = new PropertyKey(propertyName, Key);
            }
            return value;
        }

        protected T PrepareOptionValue<T>(Symbol optionName, T value)
        {
            this.ThrowIfFrozen();
            if (value is INode node && node.Key.IsNull()) {
                // We automatically provide keys for INode properties (or collection items)
                // by extending the owner's key with property name suffix 
                node.Key = new OptionKey(optionName, Key);
            }
            return value;
        }
    }

    public class Node<TKey> : Node, INode<TKey>
        where TKey : Key
    {
        private TKey _key = default!;

        [NotMapped]
        public new TKey Key {
            get => _key;
            set {
                this.ThrowIfFrozen(); 
                _key = value;
            }
        }

        protected override Key UntypedKey {
            get => Key;
            set => Key = (TKey) value;
        }

        public Node() { }
        public Node(TKey key) => Key = key;
    }
}

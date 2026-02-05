using System.Globalization;
using MessagePack;

namespace ActualLab.Api;

/// <summary>
/// A serializable list intended for use in API contracts.
/// </summary>
[DataContract, MemoryPackable(GenerateType.Collection), MessagePackObject]
public sealed partial class ApiList<T> : List<T>
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool IsEmpty => Count == 0;

    public ApiList() { }
    public ApiList(IEnumerable<T> collection) : base(collection) { }
    public ApiList(int capacity) : base(capacity) { }

    public ApiList<T> Clone() => new(this);

    public ApiList<T> With(Action<ApiList<T>> mutator)
    {
        var newList = Clone();
        mutator.Invoke(newList);
        return newList;
    }

    public override string ToString()
    {
        var sb = StringBuilderExt.Acquire();
        sb.Append('<');
        sb.Append(typeof(T).GetName());
        sb.Append(">[");
        var i = 0;
        foreach (var item in this) {
            if (i >= ApiCollectionExt.MaxToStringItems) {
#if NET6_0_OR_GREATER
                sb.Append(CultureInfo.InvariantCulture, $", ...{Count - ApiCollectionExt.MaxToStringItems} more");
#else
                sb.Append($", ...{Count - ApiCollectionExt.MaxToStringItems} more");
#endif
                break;
            }
            if (i > 0)
                sb.Append(", ");
            sb.Append(item);
            i++;
        }
        sb.Append(']');
        return sb.ToStringAndRelease();
    }
}

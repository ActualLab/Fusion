using RestEase;

namespace ActualLab.Tests.RestEase;

[BasePath("restEase")]
[SerializationMethods(Query = QuerySerializationMethod.Serialized)]
public interface IRestEaseClient : IComputeService
{
    [Get("getFromQueryImplicit")]
    public Task<string> GetFromQueryImplicit(string str, CancellationToken cancellationToken = default);
    [Get("getFromQuery")]
    public Task<string> GetFromQuery(string str, CancellationToken cancellationToken = default);
    [Get("getFromQueryComplex")]
    public Task<QueryParamModel> GetFromQueryComplex(QueryParamModel str, CancellationToken cancellationToken = default);
    [Get("getJsonString")]
    public Task<JsonString> GetJsonString(string str, CancellationToken cancellationToken = default);
    [Get("getFromPath/{str}")]
    public Task<string> GetFromPath([Path] string str, CancellationToken cancellationToken = default);

    [Post("postFromQueryImplicit")]
    public Task<JsonString> PostFromQueryImplicit(string str, CancellationToken cancellationToken = default);
    [Post("postFromQuery")]
    public Task<JsonString> PostFromQuery(string str, CancellationToken cancellationToken = default);
    [Post("postFromPath/{str}")]
    public Task<JsonString> PostFromPath([Path] string str, CancellationToken cancellationToken = default);
    [Post("postWithBody")]
    public Task<JsonString> PostWithBody([Body] StringWrapper str, CancellationToken cancellationToken = default);
    [Post("concatQueryAndPath/{b}")]
    public Task<JsonString> ConcatQueryAndPath(string a, [Path] string b, CancellationToken cancellationToken = default);
    [Post("concatPathAndBody/{a}")]
    public Task<JsonString> ConcatPathAndBody([Path] string a, [Body] string b, CancellationToken cancellationToken = default);
}

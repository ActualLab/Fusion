using ActualLab.Interception;

namespace ActualLab.Tests.Interception;

public interface IService
{
    public string One(string source);
    public int Two(string source);
    public JsonString Three();

    public Task<string> OneAsync(string source);
    public Task<int> TwoAsync(string source);
    public Task<JsonString> ThreeAsync();

    public ValueTask<string> OneXAsync(string source);
    public ValueTask<int> TwoXAsync(string source);
    public ValueTask<JsonString> ThreeXAsync();
}

public interface IView : IRequiresFullProxy
{
    public string One(string source);
    public string Two(string source);
    public string Three();

    public Task<string> OneAsync(string source);
    public Task<string> TwoAsync(string source);
    public Task<string> ThreeAsync();

    public ValueTask<string> OneXAsync(string source);
    public ValueTask<string> TwoXAsync(string source);
    public ValueTask<string> ThreeXAsync();
}

#pragma warning disable VSTHRD103

public class Service : IService
{
    public string One(string source)
        => source;

    public int Two(string source)
        => source.Length;

    public JsonString Three()
        => new("1");

    public Task<string> OneAsync(string source)
        => Task.FromResult(One(source));

    public Task<int> TwoAsync(string source)
        => Task.FromResult(Two(source));

    public Task<JsonString> ThreeAsync()
        => Task.FromResult(Three());

    public ValueTask<string> OneXAsync(string source)
        => ValueTaskExt.FromResult(One(source));

    public ValueTask<int> TwoXAsync(string source)
        => ValueTaskExt.FromResult(Two(source));

    public ValueTask<JsonString> ThreeXAsync()
        => ValueTaskExt.FromResult(Three());
}

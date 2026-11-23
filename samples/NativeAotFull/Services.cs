using ActualLab.Api;
using ActualLab.Compliance;
using ActualLab.Fusion.Extensions;

// Each method round-trips one of the types the checks in Program.cs use
public interface ITestService : IComputeService
{
    public Task<Moment> GetTime(CancellationToken cancellationToken = default);

    [ComputeMethod(AutoInvalidationDelay = 1)]
    public Task<Moment> GetTimeComputed(CancellationToken cancellationToken = default);

    [CommandHandler]
    public Task<string> OnSayHello(SayHelloCommand command, CancellationToken cancellationToken = default);

    public Task<PropertyBag<TypeSchema.Any>> EchoBag(
        PropertyBag<TypeSchema.Any> value, CancellationToken cancellationToken = default);
    public Task<PropertyBag<CustomSchema>> EchoCustomBag(
        PropertyBag<CustomSchema> value, CancellationToken cancellationToken = default);
    public Task<SanitizedString<Sanitizers.Hidden>> EchoSanitized(
        SanitizedString<Sanitizers.Hidden> value, CancellationToken cancellationToken = default);
    public Task<SanitizedString<CustomSanitizer>> EchoCustomSanitized(
        SanitizedString<CustomSanitizer> value, CancellationToken cancellationToken = default);
    public Task<Option<int>> EchoOption(Option<int> value, CancellationToken cancellationToken = default);
    public Task<int?> EchoNullable(int? value, CancellationToken cancellationToken = default);
    public Task<List<int>> EchoList(List<int> value, CancellationToken cancellationToken = default);
    public Task<Moment?> EchoNullableMoment(Moment? value, CancellationToken cancellationToken = default);
    public Task<Moment[]> EchoMomentArray(Moment[] value, CancellationToken cancellationToken = default);
    public Task<List<Moment>> EchoMomentList(List<Moment> value, CancellationToken cancellationToken = default);
    public Task<Dictionary<string, Moment>> EchoMomentMap(
        Dictionary<string, Moment> value, CancellationToken cancellationToken = default);
    public Task<ApiArray<int>> EchoApiArray(ApiArray<int> value, CancellationToken cancellationToken = default);
    public Task<PageRef<long>> EchoPageRef(PageRef<long> value, CancellationToken cancellationToken = default);
    public Task<ApiOption<int>> EchoApiOption(ApiOption<int> value, CancellationToken cancellationToken = default);
    public Task<ApiNullable<int>> EchoApiNullable(
        ApiNullable<int> value, CancellationToken cancellationToken = default);
    public Task<ApiNullable8<long>> EchoApiNullable8(
        ApiNullable8<long> value, CancellationToken cancellationToken = default);
    public Task<ApiDto> EchoDto(ApiDto value, CancellationToken cancellationToken = default);
}

public class TestService : ITestService
{
    public virtual Task<Moment> GetTime(CancellationToken cancellationToken = default)
        => Task.FromResult(Moment.Now);

    public virtual Task<Moment> GetTimeComputed(CancellationToken cancellationToken = default)
        => Task.FromResult(Moment.Now);

    public virtual Task<string> OnSayHello(SayHelloCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult($"Hello, {command.Name}");

    public virtual Task<PropertyBag<TypeSchema.Any>> EchoBag(
        PropertyBag<TypeSchema.Any> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<PropertyBag<CustomSchema>> EchoCustomBag(
        PropertyBag<CustomSchema> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<SanitizedString<Sanitizers.Hidden>> EchoSanitized(
        SanitizedString<Sanitizers.Hidden> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<SanitizedString<CustomSanitizer>> EchoCustomSanitized(
        SanitizedString<CustomSanitizer> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<Option<int>> EchoOption(Option<int> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<int?> EchoNullable(int? value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<List<int>> EchoList(List<int> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<Moment?> EchoNullableMoment(Moment? value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<Moment[]> EchoMomentArray(Moment[] value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<List<Moment>> EchoMomentList(List<Moment> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<Dictionary<string, Moment>> EchoMomentMap(
        Dictionary<string, Moment> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<ApiArray<int>> EchoApiArray(ApiArray<int> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<PageRef<long>> EchoPageRef(PageRef<long> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<ApiOption<int>> EchoApiOption(
        ApiOption<int> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<ApiNullable<int>> EchoApiNullable(
        ApiNullable<int> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<ApiNullable8<long>> EchoApiNullable8(
        ApiNullable8<long> value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public virtual Task<ApiDto> EchoDto(ApiDto value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);
}

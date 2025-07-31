using System.Diagnostics.CodeAnalysis;
using ActualLab.Conversion;
using ActualLab.Fusion.EntityFramework.Internal;
using ActualLab.Generators;

namespace ActualLab.Fusion.Authentication.Services;

public interface IDbUserIdHandler<TDbUserId>
{
    public TDbUserId New();
    public TDbUserId None { get; }

    public bool IsNone([NotNullWhen(false)] TDbUserId? userId);
    public void Require([NotNull] TDbUserId? userId);

    public string Format(TDbUserId? userId);
    public TDbUserId Parse(string userId, bool allowNone);
    public bool TryParse(string userId, bool allowNone, out TDbUserId result);
}

public class DbUserIdHandler<TDbUserId> : IDbUserIdHandler<TDbUserId>
{
    protected IConverter<string, TDbUserId?> Parser { get; init; }
    protected IConverter<TDbUserId?, string> Formatter { get; init; }
    protected Func<TDbUserId> Generator { get; init; }

    public TDbUserId None { get; init; }

    public DbUserIdHandler(IConverterProvider converters, Func<TDbUserId>? generator = null)
    {
        None = default!;
        if (typeof(TDbUserId) == typeof(string))
            None = (TDbUserId) (object) "";
        if (generator is null) {
            generator = () => default!;
            if (typeof(TDbUserId) == typeof(string)) {
                var rsg = new RandomStringGenerator(12, RandomStringGenerator.Base32Alphabet);
                generator = () => (TDbUserId) (object) rsg.Next();
            }
        }
        Parser = converters.From<string>().To<TDbUserId?>();
        Formatter = converters.From<TDbUserId?>().To<string>();
        Generator = generator;
    }

    public virtual TDbUserId New()
        => Generator();

    public virtual bool IsNone([NotNullWhen(false)] TDbUserId? userId)
        => EqualityComparer<TDbUserId>.Default.Equals(userId!, None)
            || EqualityComparer<TDbUserId>.Default.Equals(userId!, default!);

    public void Require([NotNull] TDbUserId? userId)
    {
        if (IsNone(userId))
            throw Errors.UserIdRequired();
    }

    public virtual string Format(TDbUserId? userId)
        => IsNone(userId)
            ? ""
            : Formatter.Convert(userId);

    public virtual TDbUserId Parse(string userId, bool allowNone)
    {
        if (!TryParse(userId, true, out var result))
            throw Errors.InvalidUserId();
        if (!allowNone && IsNone(result))
            throw Errors.UserIdRequired();
        return result;
    }

    public virtual bool TryParse(string userId, bool allowNone, out TDbUserId result)
    {
        result = None;
        if (userId.IsNullOrEmpty())
            return allowNone;

        if (!Parser.TryConvert(userId).IsSome(out var parsed))
            return false;

        if (parsed is null)
            return allowNone;

        result = parsed;
        return true;
    }
}

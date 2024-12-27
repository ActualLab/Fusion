using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;
using ActualLab.Requirements;

namespace ActualLab;

public abstract record Requirement
{
    public abstract bool IsSatisfiedUntyped([NotNullWhen(true)] object? value);
    public abstract void CheckUntyped([NotNull] object? value, string? targetName = null);
    public abstract Exception GetErrorUntyped(object? value, string? targetName = null);

    public static FuncRequirement<T> New<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
        (Func<T?, bool> validator, ExceptionBuilder exceptionBuilder)
        => new(validator, exceptionBuilder);
    public static FuncRequirement<T> New<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
        (Func<T?, bool> validator)
        => new(validator);
}

public abstract record Requirement<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    : Requirement
{
    private const string MustExistFieldOrPropertyName = "MustExist";
    // ReSharper disable once StaticMemberInGenericType
    private static readonly Lock MustExistLock = LockFactory.Create();
    private static volatile Requirement<T>? _mustExist;

    public static Requirement<T> MustExist {
        get {
            if (_mustExist != null)
                return _mustExist;
            lock (MustExistLock) {
                if (_mustExist != null)
                    return _mustExist;

                Requirement<T>? mustExist;
                if (typeof(IRequirementTarget).IsAssignableFrom(typeof(T))) {
                    var type = typeof(T);
                    var propertyValue = type
                        .GetField(MustExistFieldOrPropertyName, BindingFlags.Public | BindingFlags.Static)
                        ?.GetValue(null);
                    propertyValue ??= type
                        .GetProperty(MustExistFieldOrPropertyName, BindingFlags.Public | BindingFlags.Static)
                        ?.GetValue(null);
                    mustExist = propertyValue as Requirement<T>;
                    if (mustExist == null) {
                        if (propertyValue != null)
                            throw Errors.InternalError("MustExist property or field must return Requirement<T>.");

                        mustExist = MustExistRequirement<T>.Default;
                    }
                }
                else
                    mustExist = MustExistRequirement<T>.Default;
                return _mustExist = mustExist;
            }
        }
    }

    public override bool IsSatisfiedUntyped([NotNullWhen(true)] object? value)
        => IsSatisfied((T?)value);
    public override void CheckUntyped([NotNull] object? value, string? targetName = null)
        => Check((T?)value, targetName);
    public override Exception GetErrorUntyped(object? value, string? targetName = null)
        => GetError((T?)value, targetName);

    public abstract bool IsSatisfied([NotNullWhen(true)] T? value);
    public abstract Exception GetError(T? value, string? targetName = null);

    public virtual void Check([NotNull] T? value, string? targetName = null)
    {
        if (!IsSatisfied(value))
            throw GetError(value, targetName);
    }

    public Requirement<T> And(Requirement<T> secondary)
        => new JointRequirement<T>(this, secondary);

    public Requirement<T> With(ExceptionBuilder exceptionBuilder)
        => this is CustomizableRequirementBase<T> customizableRequirement
            ? customizableRequirement with { ExceptionBuilder = exceptionBuilder }
            : new CustomizableRequirement<T>(this, exceptionBuilder);
    public Requirement<T> With(string messageTemplate, Func<string, Exception>? exceptionFactory = null)
        => With(new ExceptionBuilder(messageTemplate, exceptionFactory));
    public Requirement<T> With(string messageTemplate, string targetName, Func<string, Exception>? exceptionFactory = null)
        => With(new ExceptionBuilder(messageTemplate, targetName, exceptionFactory));
    public Requirement<T> With(Func<Exception> exceptionFactory)
        => With(new ExceptionBuilder(exceptionFactory));

    public static Requirement<T> operator &(Requirement<T> primary, Requirement<T> secondary)
        => primary.And(secondary);
    public static Requirement<T> operator +(Requirement<T> requirement, ExceptionBuilder exceptionBuilder)
        => requirement.With(exceptionBuilder);
    public static Requirement<T> operator +(Requirement<T> requirement, string messageTemplate)
        => requirement.With(messageTemplate);
    public static Requirement<T> operator +(Requirement<T> requirement, Func<Exception> exceptionBuilder)
        => requirement.With(exceptionBuilder);
}

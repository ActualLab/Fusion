using ActualLab.Generators;

namespace ActualLab.Fusion;

/// <summary>
/// Provides factory methods that create <see cref="SessionFactory"/> delegates
/// producing <see cref="Session"/> instances with random string identifiers.
/// </summary>
public sealed class DefaultSessionFactory
{
    public static SessionFactory New(int length = 20, string? alphabet = null)
        => New(new RandomStringGenerator(length, alphabet));

    public static SessionFactory New(Generator<string> generator)
        => () => new Session(generator.Next());
}

using ActualLab;
using CliWrap.Builders;

namespace Build;

public static class ArgumentsBuilderExt
{
    public static ArgumentsBuilder MaybeAdd(this ArgumentsBuilder builder, string argument, bool mustAdd)
        => mustAdd ? builder.Add(argument) : builder;

    public static ArgumentsBuilder AddIfNonEmpty(this ArgumentsBuilder builder, string argument)
        => builder.MaybeAdd(argument, !argument.IsNullOrEmpty());
}

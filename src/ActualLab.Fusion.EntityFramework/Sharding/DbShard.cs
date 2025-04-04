namespace ActualLab.Fusion.EntityFramework;

public static class DbShard
{
    public const string Single = "";
    public const string Template = "__template";

    public static Func<string, bool> Validator { get; set; } = static shard => !IsSpecial(shard);

    public static bool IsSingle(string shard)
        => shard.Length == 0;

    public static bool IsTemplate(string shard)
        => string.Equals(shard, Template, StringComparison.Ordinal);

    public static bool IsSpecial(string shard)
        => IsSingle(shard) || IsTemplate(shard);

    public static bool IsValid(string shard)
        => Validator.Invoke(shard);

    public static bool IsValidOrTemplate(string shard)
        => Validator.Invoke(shard) || IsTemplate(shard);

    public static string Validate(string shard)
        => Validator.Invoke(shard) ? shard : throw new ArgumentOutOfRangeException(nameof(shard));
}

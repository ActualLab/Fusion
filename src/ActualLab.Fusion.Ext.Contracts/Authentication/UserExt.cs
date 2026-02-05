namespace ActualLab.Fusion.Authentication;

/// <summary>
/// Extension methods for <see cref="User"/>.
/// </summary>
public static class UserExt
{
    public static User OrGuest(this User? user, string? name = null)
        => user ?? User.NewGuest(name);
}

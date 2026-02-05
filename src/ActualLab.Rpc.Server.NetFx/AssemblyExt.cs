using System.Web.Http.Controllers;

namespace ActualLab.Rpc.Server;

/// <summary>
/// Extension methods for <see cref="Assembly"/> to discover Web API controller types.
/// </summary>
public static class AssemblyExt
{
    public static IEnumerable<Type> GetControllerTypes(this Assembly assembly, string? fullNamePrefixFilter = null)
    {
        var q = assembly.GetExportedTypes()
            .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
            .Where(t => typeof(IHttpController).IsAssignableFrom(t)
                || t.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase));
        if (fullNamePrefixFilter is not null)
            q = q.Where(c => (c.FullName ?? string.Empty).StartsWith(fullNamePrefixFilter, StringComparison.Ordinal));
        return q;
    }
}

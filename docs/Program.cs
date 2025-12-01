using System.Reflection;
using static System.Console;

namespace Docs;

#region PartXX_SnippetId
// This snippet is referenced from .instructions.md
#endregion

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Docs")]
[UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Docs")]
public class Program
{
    public static async Task Main(string[] args)
    {
        var types = GetPartTypes(args);
        for (var i = 0; i < types.Length; i++) {
            var type = types[i];
            if (i != 0)
                WriteLine();
            WriteLine($"---- {type.Name} started ----");

            var method = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
            if (method is null)
                throw new InvalidOperationException($"'{type.Name}' type doesn't have 'Run' method.");

            var result = method.Invoke(null, []);
            if (result is Task task)
                await task.ConfigureAwait(false);

            WriteLine($"---- {type.Name} completed ----");
        }
    }

    public static Type[] GetPartTypes(string[] args)
    {
        var types = typeof(Program).Assembly.GetTypes()
            .Where(t => t.Name.StartsWith("Part", StringComparison.Ordinal))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToArray();

        if (!args.Any()) {
            WriteLine($"Available parts: {types.Select(t => t.Name).ToDelimitedString()}");
            WriteLine("List the parts to run, Enter to run all parts: ");
            args = (ReadLine() ?? "").Split().Select(x => x.Trim()).ToArray();
            if (args.Length == 0)
                args = ["all"];
        }
        args = args.Select(a => a.ToLowerInvariant()).ToArray();

        if (args.All(a => a != "all"))
            types = types.Where(t => args.Any(a => a == t.Name.ToLowerInvariant())).ToArray();
        return types;
    }
}

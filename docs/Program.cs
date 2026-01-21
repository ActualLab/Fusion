using static System.Console;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming

namespace Docs;

#region PartXX_SnippetId
// This snippet is referenced from .instructions.md
#endregion

/// <summary>
/// Base class for all documentation parts.
/// Each part demonstrates a specific feature or concept.
/// </summary>
public abstract class DocPart
{
    /// <summary>
    /// Runs all snippets for this documentation part.
    /// </summary>
    public abstract Task Run();

    /// <summary>
    /// Outputs a snippet start marker to identify snippet output in the console.
    /// </summary>
    /// <param name="snippetName">The name of the snippet being executed.</param>
    protected static void StartSnippetOutput(string snippetName)
        => WriteLine($"---- {snippetName} ----");
}

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
            WriteLine($"---- Part {type.Name} started ----");

            var part = (DocPart)Activator.CreateInstance(type)!;
            await part.Run().ConfigureAwait(false);

            WriteLine($"---- Part {type.Name} completed ----");
        }
    }

    public static Type[] GetPartTypes(string[] args)
    {
        var types = typeof(Program).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(DocPart)) && !t.IsAbstract)
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

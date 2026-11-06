using System.Text;

namespace ActualLab.Rpc.TypeScript;

/// <summary>
/// Helpers producing TypeScript identifiers and literals.
/// </summary>
public static class TypeScriptSyntax
{
    private static readonly HashSet<string> ReservedWords = new(StringComparer.Ordinal) {
        "as", "await", "break", "case", "catch", "class", "const", "continue", "debugger", "default",
        "delete", "do", "else", "enum", "export", "extends", "false", "finally", "for", "function",
        "if", "implements", "import", "in", "instanceof", "interface", "let", "new", "null", "package",
        "private", "protected", "public", "return", "static", "super", "switch", "this", "throw", "true",
        "try", "typeof", "var", "void", "while", "with", "yield",
    };

    public static string Quote(string value)
    {
        var sb = new StringBuilder(value.Length + 2).Append('\'');
        foreach (var c in value)
            sb.Append(c switch {
                '\\' => @"\\",
                '\'' => @"\'",
                '\n' => @"\n",
                '\r' => @"\r",
                '\t' => @"\t",
                _ => c.ToString(),
            });
        return sb.Append('\'').ToString();
    }

    public static string PropertyName(string name)
        => IsIdentifier(name) ? name : Quote(name);

    public static string ParameterName(string name)
        => ReservedWords.Contains(name) ? name + "_" : name;

    public static bool IsIdentifier(string name)
    {
        if (name.Length == 0 || char.IsDigit(name[0]))
            return false;

        foreach (var c in name)
            if (!(char.IsAsciiLetterOrDigit(c) || c is '_' or '$'))
                return false;

        return true;
    }
}

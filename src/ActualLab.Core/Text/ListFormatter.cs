using System.Text;
using Cysharp.Text;

namespace ActualLab.Text;

/// <summary>
/// A ref struct that formats a sequence of strings into a delimited list
/// using <see cref="ListFormat"/>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public ref struct ListFormatter
{
    public ListFormat Format => new(Delimiter, Escape);
    public readonly char Delimiter;
    public readonly char Escape;
    public StringBuilder OutputBuilder;
    public int ItemIndex;
    public string Output => OutputBuilder.ToString();

#pragma warning disable RCS1242
    internal ListFormatter(
        ListFormat format,
        int itemIndex)
#pragma warning restore RCS1242
    {
        Delimiter = format.Delimiter;
        Escape = format.Escape;
        OutputBuilder = StringBuilderExt.Acquire();
        ItemIndex = itemIndex;
    }

    public void Dispose()
        => OutputBuilder.Release();

    public void Append(string item)
    {
        if (ItemIndex++ != 0)
            OutputBuilder.Append(Delimiter);
        foreach (var c in item) {
            if (c == Delimiter || c == Escape)
                OutputBuilder.Append(Escape);
            OutputBuilder.Append(c);
        }
    }

    public void Append(ReadOnlySpan<char> item)
    {
        if (ItemIndex++ != 0)
            OutputBuilder.Append(Delimiter);
        foreach (var c in item) {
            if (c == Delimiter || c == Escape)
                OutputBuilder.Append(Escape);
            OutputBuilder.Append(c);
        }
    }

    public void AppendEnd()
    {
        if (ItemIndex == 0)
            // Special case: single Escape = an empty list
            OutputBuilder.Append(Escape);
    }

    public void Append(IEnumerator<string> enumerator, bool appendEndOfList = true)
    {
        while (enumerator.MoveNext())
            Append(enumerator.Current!);
        if (appendEndOfList)
            AppendEnd();
    }

    public void Append(IEnumerable<string> sequence, bool appendEndOfList = true)
    {
        foreach (var item in sequence)
            Append(item);
        if (appendEndOfList)
            AppendEnd();
    }
}

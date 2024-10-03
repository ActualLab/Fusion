using System.Text;

namespace ActualLab.Text;

public static class EncodingExt
{
    public static readonly Encoding Utf8NoBom = new UTF8Encoding(false); // UTF8, but w/o BOM
}

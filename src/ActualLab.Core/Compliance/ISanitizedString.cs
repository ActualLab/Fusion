namespace ActualLab.Compliance;

/// <summary>
/// The non-generic face of <see cref="SanitizedString{TSanitizer}"/>, so a serializer or a log
/// formatter can read the raw value without knowing which <see cref="Sanitizer"/> guards it.
/// </summary>
public interface ISanitizedString : ISanitized
{
    public string Value { get; }
    public Sanitizer Sanitizer { get; }
}

namespace ActualLab.Serialization;

/// <summary>
/// Indicates that type's ToString() can be deserialized
/// with System.Text.Json / JSON.NET deserializers.
/// </summary>
public interface IHasToStringProducingJson;

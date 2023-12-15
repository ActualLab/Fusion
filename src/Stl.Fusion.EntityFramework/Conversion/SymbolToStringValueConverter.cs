using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ActualLab.Fusion.EntityFramework.Conversion;

public class SymbolToStringValueConverter(ConverterMappingHints? mappingHints = null)
    : ValueConverter<Symbol, string>(
        v => v.Value,
        v => new Symbol(v),
        mappingHints);

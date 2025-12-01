using System.ComponentModel.DataAnnotations;
using ActualLab.Internal;
using Microsoft.Extensions.Configuration;

namespace ActualLab.DependencyInjection;

public static class ConfigurationExt
{
    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static TSettings GetSettings<TSettings>(this IConfiguration configuration, bool mustValidate = true)
        where TSettings : class, new()
        => configuration.GetSettings<TSettings>(null, mustValidate);

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static TSettings GetSettings<TSettings>(this IConfiguration configuration,
        string? sectionName,
        bool mustValidate = true)
        where TSettings : class, new()
    {
        var settingsType = typeof(TSettings);
        var altSectionName = (string?)null;
        if (sectionName is null) {
            sectionName = settingsType.Name;
            var plusIndex = sectionName.IndexOf('+', StringComparison.Ordinal);
            if (plusIndex >= 0)
                sectionName = sectionName[(plusIndex + 1)..];
            altSectionName = sectionName.TrimSuffixes("Settings", "Cfg", "Config", "Configuration");
        }
        var settings = new TSettings();
        var section = configuration.GetSection(sectionName);
        if (!section.Exists() && altSectionName is not null)
            section = configuration.GetSection(altSectionName);
        section.Bind(settings);
        if (mustValidate) {
            var validationContext = new ValidationContext(settings, null);
            Validator.ValidateObject(settings, validationContext);
        }
        return settings;
    }
}

using System;
using Microsoft.Extensions.Configuration;

namespace PetToys.TemplatedConfigurationProvider;

/// <summary>
/// Options to configure TemplatedConfigurationProvider.
/// </summary>
public sealed class TemplatedConfigurationOptions
{
    /// <summary>
    /// Gets or sets the starting character of the pattern. Removed when replaced.
    /// </summary>
    public char TemplateCharacterStart { get; set; } = '{';

    /// <summary>
    /// Gets or sets the end character of the pattern. Removed when replaced.
    /// </summary>
    public char TemplateCharacterEnd { get; set; } = '}';

    /// <summary>
    /// Validates the configured delimiter characters, throwing
    /// <see cref="ArgumentException"/> for combinations the parser cannot handle.
    /// </summary>
    internal void Validate()
    {
        if (TemplateCharacterStart == TemplateCharacterEnd)
        {
            throw new ArgumentException(
                $"{nameof(TemplateCharacterStart)} and {nameof(TemplateCharacterEnd)} must differ.",
                nameof(TemplateCharacterStart));
        }

        ValidateCharacter(TemplateCharacterStart, nameof(TemplateCharacterStart));
        ValidateCharacter(TemplateCharacterEnd, nameof(TemplateCharacterEnd));
    }

    private static void ValidateCharacter(char value, string propertyName)
    {
        if (value.ToString() == ConfigurationPath.KeyDelimiter)
        {
            throw new ArgumentException(
                $"{propertyName} ('{value}') conflicts with the configuration key delimiter ('{ConfigurationPath.KeyDelimiter}').",
                propertyName);
        }

        if (char.IsWhiteSpace(value) || char.IsControl(value))
        {
            throw new ArgumentException(
                $"{propertyName} must not be a whitespace or control character.",
                propertyName);
        }
    }
}

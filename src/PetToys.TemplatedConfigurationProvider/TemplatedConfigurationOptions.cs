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
    /// Gets or sets a value indicating whether unresolved placeholders should
    /// throw during configuration loading instead of passing through unchanged.
    /// </summary>
    public bool ThrowOnUnresolvedPlaceholders { get; set; }

    /// <summary>
    /// Gets or sets the separator that splits a placeholder into a key and an
    /// inline default value, conventionally <c>":-"</c> so that
    /// <c>{Db:Host:-localhost}</c> falls back to <c>localhost</c>. When
    /// <see langword="null"/> (the default) the syntax is disabled and a
    /// placeholder is read as a configuration key in full.
    /// </summary>
    public string? DefaultValueSeparator { get; set; }

    /// <summary>
    /// Validates the configured delimiter characters and the default value
    /// separator, throwing <see cref="ArgumentException"/> for combinations the
    /// parser cannot handle.
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
        ValidateDefaultValueSeparator();
    }

    private void ValidateDefaultValueSeparator()
    {
        if (DefaultValueSeparator is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(DefaultValueSeparator))
        {
            throw new ArgumentException(
                $"{nameof(DefaultValueSeparator)} must not be empty or whitespace. Set it to null to disable inline default values.",
                nameof(DefaultValueSeparator));
        }

        if (DefaultValueSeparator.Contains(TemplateCharacterStart)
            || DefaultValueSeparator.Contains(TemplateCharacterEnd))
        {
            throw new ArgumentException(
                $"{nameof(DefaultValueSeparator)} ('{DefaultValueSeparator}') must not contain a template delimiter ('{TemplateCharacterStart}' or '{TemplateCharacterEnd}').",
                nameof(DefaultValueSeparator));
        }

        if (DefaultValueSeparator == ConfigurationPath.KeyDelimiter)
        {
            throw new ArgumentException(
                $"{nameof(DefaultValueSeparator)} ('{DefaultValueSeparator}') conflicts with the configuration key delimiter ('{ConfigurationPath.KeyDelimiter}').",
                nameof(DefaultValueSeparator));
        }
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

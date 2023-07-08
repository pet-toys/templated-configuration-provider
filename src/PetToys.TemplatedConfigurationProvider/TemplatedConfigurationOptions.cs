#nullable enable

namespace PetToys.TemplatedConfigurationProvider
{
    public sealed class TemplatedConfigurationOptions
    {
        public char TemplateCharacterStart { get; set; } = '{';

        public char TemplateCharacterEnd { get; set; } = '}';
    }
}

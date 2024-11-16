namespace PetToys.TemplatedConfigurationProvider
{
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
    }
}

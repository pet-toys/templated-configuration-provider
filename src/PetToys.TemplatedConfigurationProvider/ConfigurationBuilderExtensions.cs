using System;
using Microsoft.Extensions.Configuration;

namespace PetToys.TemplatedConfigurationProvider
{
    /// <summary>
    /// IConfigurationBuilder extension methods for the TemplatedConfigurationProvider.
    /// </summary>
    public static class ConfigurationBuilderExtensions
    {
        /// <summary>
        /// Adds the templated configuration provider to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to add to.</param>
        /// <param name="optionBuilder">Configures the options.</param>
        /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddTemplatedConfiguration(this IConfigurationBuilder builder, Action<TemplatedConfigurationOptions>? optionBuilder = null)
        {
            var options = new TemplatedConfigurationOptions();
            optionBuilder?.Invoke(options);

            return builder.Add(new TemplatedConfigurationSource(options));
        }
    }
}

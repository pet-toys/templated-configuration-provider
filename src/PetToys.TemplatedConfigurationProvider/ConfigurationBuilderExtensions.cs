#nullable enable

using System;
using Microsoft.Extensions.Configuration;

namespace PetToys.TemplatedConfigurationProvider
{
    public static class ConfigurationBuilderExtensions
    {
        public static IConfigurationBuilder AddTemplatedConfiguration(this IConfigurationBuilder builder, Action<TemplatedConfigurationOptions>? optionBuilder = null)
        {
            var options = new TemplatedConfigurationOptions();
            optionBuilder?.Invoke(options);

            return builder.Add(new TemplatedConfigurationSource(options));
        }
    }
}

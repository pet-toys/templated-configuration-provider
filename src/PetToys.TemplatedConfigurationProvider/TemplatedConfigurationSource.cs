#nullable enable

using Microsoft.Extensions.Configuration;

namespace PetToys.TemplatedConfigurationProvider
{
    internal sealed class TemplatedConfigurationSource : IConfigurationSource
    {
        private readonly TemplatedConfigurationOptions _options;

        public TemplatedConfigurationSource(TemplatedConfigurationOptions options)
        {
            _options = options;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder) =>
            new TemplatedConfigurationProvider(_options, builder);
    }
}

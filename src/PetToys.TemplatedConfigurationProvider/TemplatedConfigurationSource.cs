using Microsoft.Extensions.Configuration;

namespace PetToys.TemplatedConfigurationProvider
{
    internal sealed class TemplatedConfigurationSource(
        TemplatedConfigurationOptions options)
        : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new TemplatedConfigurationProvider(options, builder);
        }
    }
}

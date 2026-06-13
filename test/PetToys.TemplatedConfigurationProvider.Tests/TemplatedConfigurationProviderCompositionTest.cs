using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Xunit;

namespace PetToys.TemplatedConfigurationProvider.Tests;

public sealed class TemplatedConfigurationProviderCompositionTest : IDisposable
{
    private readonly IConfigurationRoot _configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sample"] = "Value",
        })
        .AddTemplatedConfiguration()
        .Build();

    [Fact]
    public void AddTemplatedConfiguration_AppendsTemplatedProviderAfterExistingSources()
    {
        var providers = _configuration.Providers.ToArray();

        providers.Length.Should().Be(2);
        providers[0].GetType().Should().Be<MemoryConfigurationProvider>();
        providers[1].GetType().Should().Be<TemplatedConfigurationProvider>();
    }

    [Fact]
    public void AddTemplatedConfiguration_ReturnsSameBuilderInstance()
    {
        var builder = new ConfigurationBuilder();

        var result = builder.AddTemplatedConfiguration();

        result.Should().BeSameAs(builder);
    }

    public void Dispose() => ((IDisposable)_configuration).Dispose();
}

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
    private readonly List<IDisposable> _roots = [];

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

    [Fact]
    public void Resolution_SourceRegisteredAfterProvider_DoesNotSatisfyPlaceholder()
    {
        // The templated value precedes the provider; the key it references is
        // supplied only by a source registered *after* AddTemplatedConfiguration.
        // That later source must not feed resolution, so the placeholder is left
        // verbatim and the templated provider emits no value for the key.
        var root = Build(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Svc:Url"] = "https://{Svc:Tenant}.example.com",
            })
            .AddTemplatedConfiguration()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Svc:Tenant"] = "acme",
            }));

        var templated = root.Providers.OfType<TemplatedConfigurationProvider>().Single();
        templated.TryGet("Svc:Url", out _).Should().BeFalse();
        root["Svc:Url"].Should().Be("https://{Svc:Tenant}.example.com");
    }

    [Fact]
    public void Resolution_SourceRegisteredBeforeProvider_SatisfiesPlaceholder()
    {
        // Regression guard: both the templated value and its referenced key are
        // supplied by sources preceding the provider, so resolution still works.
        var root = Build(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Svc:Url"] = "https://{Svc:Tenant}.example.com",
                ["Svc:Tenant"] = "acme",
            })
            .AddTemplatedConfiguration());

        root["Svc:Url"].Should().Be("https://acme.example.com");
    }

    private IConfigurationRoot Build(IConfigurationBuilder builder)
    {
        var root = builder.Build();
        _roots.Add((IDisposable)root);
        return root;
    }

    public void Dispose()
    {
        ((IDisposable)_configuration).Dispose();
        foreach (var root in _roots)
        {
            root.Dispose();
        }
    }
}

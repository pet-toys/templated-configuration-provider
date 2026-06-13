using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PetToys.TemplatedConfigurationProvider.Tests;

public sealed class TemplatedConfigurationProviderDisposeTest
{
    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var provider = BuildProvider(out _);

        var act = () =>
        {
            provider.Dispose();
            provider.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void SourceChange_BeforeDispose_TriggersReload()
    {
        // Sanity check that the harness actually drives a reload, so the
        // post-dispose assertion below cannot pass vacuously.
        var provider = BuildProvider(out var source);
        var token = provider.GetReloadToken();

        source.SetValue("Svc:Tenant", "changed");

        token.HasChanged.Should().BeTrue();
        provider.Dispose();
    }

    [Fact]
    public void SourceChange_AfterDispose_TriggersNoReload()
    {
        var provider = BuildProvider(out var source);
        var token = provider.GetReloadToken();

        provider.Dispose();
        source.SetValue("Svc:Tenant", "changed");

        token.HasChanged.Should().BeFalse();
    }

    private static TemplatedConfigurationProvider BuildProvider(out TriggerableConfigurationSource source)
    {
        source = new TriggerableConfigurationSource(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Svc:Url"] = "https://{Svc:Tenant}.example.com",
            ["Svc:Tenant"] = "acme",
        });

        var builder = new ConfigurationBuilder();
        builder.Sources.Add(source);

        var provider = new TemplatedConfigurationProvider(new TemplatedConfigurationOptions(), builder);
        provider.Load();
        return provider;
    }
}

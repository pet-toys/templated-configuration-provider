using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PetToys.TemplatedConfigurationProvider.Tests;

public sealed class TemplatedConfigurationProviderLoadTest
{
    [Fact]
    public void Load_AfterSourceKeyRemoved_DropsStaleTemplatedKey()
    {
        var provider = BuildProvider(out var source);

        provider.TryGet("Svc:Url", out var initial).Should().BeTrue();
        initial.Should().Be("https://acme.example.com");

        // Removing the referenced placeholder source key means "Svc:Url" no
        // longer produces a templated value; a re-Load must drop the stale key.
        source.RemoveQuiet("Svc:Tenant");
        provider.Load();

        provider.TryGet("Svc:Url", out _).Should().BeFalse();
    }

    [Fact]
    public void Load_AfterPlaceholderValueChanged_ReflectsNewValue()
    {
        var provider = BuildProvider(out var source);

        source.SetQuiet("Svc:Tenant", "contoso");
        provider.Load();

        provider.TryGet("Svc:Url", out var value).Should().BeTrue();
        value.Should().Be("https://contoso.example.com");
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

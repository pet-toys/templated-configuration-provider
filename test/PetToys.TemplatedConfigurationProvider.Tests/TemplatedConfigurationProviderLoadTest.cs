using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Xunit;

namespace PetToys.TemplatedConfigurationProvider.Tests;

public sealed class TemplatedConfigurationProviderLoadTest
{
    private static Dictionary<string, string?> SampleData() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Svc:Url"] = "https://{Svc:Tenant}.example.com",
        ["Svc:Tenant"] = "acme",
    };

    [Fact]
    public void Load_AfterSourceKeyRemoved_DropsStaleTemplatedKey()
    {
        using var harness = new TemplatedProviderHarness(SampleData());

        harness.Provider.TryGet("Svc:Url", out var initial).Should().BeTrue();
        initial.Should().Be("https://acme.example.com");

        // Removing the referenced placeholder source key means "Svc:Url" no
        // longer produces a templated value; a re-Load must drop the stale key.
        harness.Source.RemoveQuiet("Svc:Tenant");
        harness.Provider.Load();

        harness.Provider.TryGet("Svc:Url", out _).Should().BeFalse();
    }

    [Fact]
    public void Load_AfterPlaceholderValueChanged_ReflectsNewValue()
    {
        using var harness = new TemplatedProviderHarness(SampleData());

        harness.Source.SetQuiet("Svc:Tenant", "contoso");
        harness.Provider.Load();

        harness.Provider.TryGet("Svc:Url", out var value).Should().BeTrue();
        value.Should().Be("https://contoso.example.com");
    }
}

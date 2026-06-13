using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Xunit;

namespace PetToys.TemplatedConfigurationProvider.Tests;

public sealed class TemplatedConfigurationProviderDisposeTest
{
    private static Dictionary<string, string?> SampleData() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Svc:Url"] = "https://{Svc:Tenant}.example.com",
        ["Svc:Tenant"] = "acme",
    };

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var harness = new TemplatedProviderHarness(SampleData());

        var act = () =>
        {
            harness.Provider.Dispose();
            harness.Provider.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void SourceChange_BeforeDispose_TriggersReload()
    {
        // Sanity check that the harness actually drives a reload, so the
        // post-dispose assertion below cannot pass vacuously.
        using var harness = new TemplatedProviderHarness(SampleData());
        var token = harness.Provider.GetReloadToken();

        harness.Source.SetValue("Svc:Tenant", "changed");

        token.HasChanged.Should().BeTrue();
    }

    [Fact]
    public void SourceChange_AfterDispose_TriggersNoReload()
    {
        using var harness = new TemplatedProviderHarness(SampleData());
        var token = harness.Provider.GetReloadToken();

        harness.Provider.Dispose();
        harness.Source.SetValue("Svc:Tenant", "changed");

        token.HasChanged.Should().BeFalse();
    }
}

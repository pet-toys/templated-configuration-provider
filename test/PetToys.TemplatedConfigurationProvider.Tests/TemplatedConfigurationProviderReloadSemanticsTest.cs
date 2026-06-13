using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Xunit;

namespace PetToys.TemplatedConfigurationProvider.Tests;

/// <summary>
/// Drives the change-token <c>Reload</c> path deterministically through a
/// <see cref="TriggerableConfigurationSource"/>, asserting both the resulting
/// data and whether a reload was actually signalled.
/// </summary>
public sealed class TemplatedConfigurationProviderReloadSemanticsTest
{
    [Fact]
    public void Reload_PlaceholderValueChanged_UpdatesValueAndSignals()
    {
        using var harness = new TemplatedProviderHarness(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Svc:Url"] = "https://{Svc:Tenant}.example.com",
            ["Svc:Tenant"] = "acme",
        });
        var token = harness.Provider.GetReloadToken();

        harness.Source.SetValue("Svc:Tenant", "contoso");

        token.HasChanged.Should().BeTrue();
        harness.Provider.TryGet("Svc:Url", out var value).Should().BeTrue();
        value.Should().Be("https://contoso.example.com");
    }

    [Fact]
    public void Reload_SourceKeyRemoved_DropsStaleKeyAndSignals()
    {
        using var harness = new TemplatedProviderHarness(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Svc:Url"] = "https://{Svc:Tenant}.example.com",
            ["Svc:Tenant"] = "acme",
        });
        var token = harness.Provider.GetReloadToken();

        harness.Source.Remove("Svc:Tenant");

        token.HasChanged.Should().BeTrue();
        harness.Provider.TryGet("Svc:Url", out _).Should().BeFalse();
    }

    [Fact]
    public void Reload_PlaceholderKeyBecomesResolvable_AddsKeyAndSignals()
    {
        // Regression guard: the previous merge-based Reload only updated or
        // deleted keys already present in Data, so a value that newly becomes
        // a resolvable template at runtime was silently dropped.
        using var harness = new TemplatedProviderHarness(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Svc:Url"] = "https://plain.example.com",
            ["Svc:Tenant"] = "acme",
        });
        harness.Provider.TryGet("Svc:Url", out _).Should().BeFalse();
        var token = harness.Provider.GetReloadToken();

        harness.Source.SetValue("Svc:Url", "https://{Svc:Tenant}.example.com");

        token.HasChanged.Should().BeTrue();
        harness.Provider.TryGet("Svc:Url", out var value).Should().BeTrue();
        value.Should().Be("https://acme.example.com");
    }

    [Fact]
    public void Reload_NoTemplatedChange_DoesNotSignal()
    {
        using var harness = new TemplatedProviderHarness(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Svc:Url"] = "https://{Svc:Tenant}.example.com",
            ["Svc:Tenant"] = "acme",
        });
        var token = harness.Provider.GetReloadToken();

        // A non-template, unreferenced key changes in the source: the computed
        // templated set is unchanged, so no reload must propagate.
        harness.Source.SetValue("Svc:Note", "hello");

        token.HasChanged.Should().BeFalse();
        harness.Provider.TryGet("Svc:Url", out var value).Should().BeTrue();
        value.Should().Be("https://acme.example.com");
    }
}

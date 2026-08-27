using System;
using System.Collections.Generic;
using System.Reflection;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
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

    [Fact]
    public void Dispose_ChangeTokenRegistrationThrows_StillDisposesInnerRoot()
    {
        // Nothing in the real change-token registration can be made to throw
        // from the outside, so the failure is injected into the private field
        // to exercise the defensive path.
        var source = new DisposableConfigurationSource();
        var options = new TemplatedConfigurationOptions();
        var builder = new ConfigurationBuilder();
        builder.Sources.Add(source);

        var templatedSource = new TemplatedConfigurationSource(options);
        builder.Sources.Add(templatedSource);

        var provider = new TemplatedConfigurationProvider(options, builder, templatedSource);
        typeof(TemplatedConfigurationProvider)
            .GetField("_changeTokenRegistration", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(provider, new ThrowingDisposable());

        var act = provider.Dispose;

        act.Should().Throw<InvalidOperationException>().WithMessage("unsubscribe failed");
        source.ProviderDisposed.Should().BeTrue();
    }

    private sealed class ThrowingDisposable : IDisposable
    {
        public void Dispose() => throw new InvalidOperationException("unsubscribe failed");
    }
}

using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PetToys.TemplatedConfigurationProvider.Tests;

/// <summary>
/// Characterization tests pinning down the resolution semantics on edge inputs:
/// single-pass (non-recursive) substitution, case-insensitive placeholder keys,
/// unbalanced / empty / unresolved delimiters, null vs empty targets, and the
/// reload path under custom delimiters.
/// </summary>
public sealed class TemplatedConfigurationProviderResolutionTest : IDisposable
{
    private readonly List<IDisposable> _roots = [];

    [Fact]
    public void Resolution_TransitiveReference_ResolvesOnlyOneLevel()
    {
        // A placeholder is replaced with the referenced key's RAW source value,
        // not its templated value — substitution is single-pass.
        var config = Build(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Chain:A"] = "{Chain:B}",
            ["Chain:B"] = "{Chain:C}",
            ["Chain:C"] = "leaf",
        });

        config["Chain:A"].Should().Be("{Chain:C}");
        config["Chain:B"].Should().Be("leaf");
    }

    [Fact]
    public void Resolution_SelfReference_DoesNotRecurse()
    {
        var config = Build(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Self:Key"] = "{Self:Key}",
        });

        config["Self:Key"].Should().Be("{Self:Key}");
    }

    [Fact]
    public void Resolution_PlaceholderKey_IsCaseInsensitive()
    {
        var config = Build(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ci:Url"] = "https://{ci:TENANT}.example.com",
            ["Ci:Tenant"] = "acme",
        });

        config["Ci:Url"].Should().Be("https://acme.example.com");
    }

    [Theory]
    [InlineData("abc{def", "abc{def")]       // opening delimiter only
    [InlineData("abc}def", "abc}def")]       // closing delimiter only
    [InlineData("{Missing}", "{Missing}")]   // balanced but no matching key
    public void Resolution_UnbalancedOrUnresolved_PassesThroughVerbatim(string raw, string expected)
    {
        var config = Build(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["U:Value"] = raw,
        });

        config["U:Value"].Should().Be(expected);
    }

    [Fact]
    public void Resolution_StrictMode_UnresolvedPlaceholder_Throws()
    {
        var act = () => Build(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Svc:Url"] = "https://{Svc:Missing}.example.com",
            },
            opt => opt.ThrowOnUnresolvedPlaceholders = true);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Svc:Url*Svc:Missing*");
    }

    [Fact]
    public void Resolution_StrictMode_ResolvedPlaceholder_Resolves()
    {
        var config = Build(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Svc:Url"] = "https://{Svc:Tenant}.example.com",
                ["Svc:Tenant"] = "acme",
            },
            opt => opt.ThrowOnUnresolvedPlaceholders = true);

        config["Svc:Url"].Should().Be("https://acme.example.com");
    }

    [Theory]
    [InlineData("https://{Svc:Missing.example.com")]
    [InlineData("https://Svc:Missing}.example.com")]
    [InlineData("https://}Svc:Missing{.example.com")]
    public void Resolution_StrictMode_UnbalancedDelimiter_PassesThroughVerbatim(string raw)
    {
        var config = Build(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Svc:Url"] = raw,
            },
            opt => opt.ThrowOnUnresolvedPlaceholders = true);

        config["Svc:Url"].Should().Be(raw);
    }

    [Fact]
    public void Resolution_EmptyPlaceholder_PassesThroughVerbatim()
    {
        var config = Build(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["E:Value"] = "a{}b",
        });

        config["E:Value"].Should().Be("a{}b");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Resolution_NullOrEmptyTarget_ResolvesToEmptyString(string? target)
    {
        var config = Build(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["N:Value"] = "x{N:Ref}y",
            ["N:Ref"] = target,
        });

        config["N:Value"].Should().Be("xy");
    }

    [Fact]
    public void Resolution_RootAndSectionKeyCollide_PrefersRootValue()
    {
        // The lookup starts at the root and walks toward the value's own
        // section, returning the first match — so a root-level key wins over a
        // nearer, section-scoped one.
        var config = Build(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Auth:Authority"] = "https://host/{TenantId}/",
            ["Auth:Authority:TenantId"] = "near",
            ["TenantId"] = "root",
        });

        config["Auth:Authority"].Should().Be("https://host/root/");
    }

    [Fact]
    public void Resolution_MultiplePlaceholders_ResolveIndependently()
    {
        var config = Build(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["M:Value"] = "a{M:Empty}b{M:Filled}c",
            ["M:Empty"] = string.Empty,
            ["M:Filled"] = "x",
        });

        config["M:Value"].Should().Be("abxc");
    }

    [Fact]
    public void Reload_CustomDelimiters_DropsStaleKeyAfterSourceRemoval()
    {
        using var harness = new TemplatedProviderHarness(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Svc:Url"] = "https://<Svc:Tenant>.example.com",
                ["Svc:Tenant"] = "acme",
            },
            opt =>
            {
                opt.TemplateCharacterStart = '<';
                opt.TemplateCharacterEnd = '>';
            });

        harness.Provider.TryGet("Svc:Url", out var value).Should().BeTrue();
        value.Should().Be("https://acme.example.com");

        harness.Source.Remove("Svc:Tenant");

        harness.Provider.TryGet("Svc:Url", out _).Should().BeFalse();
    }

    private IConfiguration Build(
        IDictionary<string, string?> data,
        Action<TemplatedConfigurationOptions>? configure = null)
    {
        var root = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .AddTemplatedConfiguration(configure)
            .Build();
        _roots.Add((IDisposable)root);
        return root;
    }

    public void Dispose()
    {
        foreach (var root in _roots)
        {
            root.Dispose();
        }
    }
}

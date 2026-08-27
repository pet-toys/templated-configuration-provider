using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PetToys.TemplatedConfigurationProvider.Tests;

/// <summary>
/// Covers the inline default value segment: how a placeholder body is split,
/// when the default is substituted, and how it interacts with emission, strict
/// mode, reload and custom delimiters. The syntax is inert until
/// <see cref="TemplatedConfigurationOptions.DefaultValueSeparator"/> is set,
/// which the last group of tests pins down.
/// </summary>
public sealed class TemplatedConfigurationProviderDefaultValueTest : IDisposable
{
    private const string Separator = ":-";

    private readonly List<IDisposable> _roots = [];

    [Fact]
    public void Default_MissingKey_SubstitutesDefault()
    {
        var config = BuildWithSeparator(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Db:Connection"] = "Server={Db:Host:-localhost};Database=app",
        });

        config["Db:Connection"].Should().Be("Server=localhost;Database=app");
    }

    [Fact]
    public void Default_PresentKey_WinsOverDefault()
    {
        var config = BuildWithSeparator(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Db:Connection"] = "Server={Db:Host:-localhost};Database=app",
            ["Db:Host"] = "db.internal",
        });

        config["Db:Connection"].Should().Be("Server=db.internal;Database=app");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Default_EmptyResolution_FallsBackToDefault(string? target)
    {
        // Unlike a placeholder without a default -- which substitutes the empty
        // string -- a default states that empty is not an acceptable answer.
        var config = BuildWithSeparator(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Db:Connection"] = "Server={Db:Host:-localhost}",
            ["Db:Host"] = target,
        });

        config["Db:Connection"].Should().Be("Server=localhost");
    }

    [Fact]
    public void Default_EmptyDefault_ErasesPlaceholder()
    {
        var config = BuildWithSeparator(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Svc:Suffix"] = "name{Svc:Missing:-}",
        });

        config["Svc:Suffix"].Should().Be("name");
    }

    [Fact]
    public void Default_SeparatorInsideDefault_SplitsOnFirstOccurrence()
    {
        var config = BuildWithSeparator(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Svc:Value"] = "{Missing:-a:-b}",
        });

        config["Svc:Value"].Should().Be("a:-b");
    }

    [Fact]
    public void Default_DefaultText_IsNotResolvedFurther()
    {
        var config = BuildWithSeparator(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Svc:Value"] = "{Missing:-{Other}",
            ["Other"] = "resolved",
        });

        config["Svc:Value"].Should().Be("{Other");
    }

    [Fact]
    public void Default_KeyPart_StillUsesHierarchicalLookup()
    {
        var config = BuildWithSeparator(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Auth:Authority"] = "https://host/{TenantId:-common}/",
            ["Auth:TenantId"] = "acme",
        });

        config["Auth:Authority"].Should().Be("https://host/acme/");
    }

    [Fact]
    public void Default_ValueResolvedOnlyByDefault_IsEmittedByTheProvider()
    {
        using var harness = new TemplatedProviderHarness(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Db:Connection"] = "Server={Db:Host:-localhost}",
            },
            opt => opt.DefaultValueSeparator = Separator);

        harness.Provider.TryGet("Db:Connection", out var value).Should().BeTrue();
        value.Should().Be("Server=localhost");
    }

    [Fact]
    public void Default_StrictMode_PlaceholderWithDefault_DoesNotThrow()
    {
        var config = Build(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Svc:Url"] = "https://{Svc:Missing:-fallback}.example.com",
            },
            opt =>
            {
                opt.DefaultValueSeparator = Separator;
                opt.ThrowOnUnresolvedPlaceholders = true;
            });

        config["Svc:Url"].Should().Be("https://fallback.example.com");
    }

    [Fact]
    public void Default_StrictMode_PlaceholderWithoutDefault_StillThrows()
    {
        var act = () => Build(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Svc:Url"] = "https://{Svc:Missing}.example.com",
            },
            opt =>
            {
                opt.DefaultValueSeparator = Separator;
                opt.ThrowOnUnresolvedPlaceholders = true;
            });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Svc:Url*Svc:Missing*");
    }

    [Fact]
    public void Default_ReferencedKeyAppears_ReplacesDefaultAndSignalsReload()
    {
        using var harness = new TemplatedProviderHarness(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Db:Connection"] = "Server={Db:Host:-localhost}",
            },
            opt => opt.DefaultValueSeparator = Separator);

        harness.Provider.TryGet("Db:Connection", out var initial).Should().BeTrue();
        initial.Should().Be("Server=localhost");

        var token = harness.Provider.GetReloadToken();

        harness.Source.SetValue("Db:Host", "db.internal");

        harness.Provider.TryGet("Db:Connection", out var value).Should().BeTrue();
        value.Should().Be("Server=db.internal");
        token.HasChanged.Should().BeTrue();
    }

    [Fact]
    public void Default_CustomDelimiters_SubstitutesDefault()
    {
        var config = Build(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Svc:Url"] = "https://<Svc:Missing:-fallback>.example.com",
            },
            opt =>
            {
                opt.TemplateCharacterStart = '<';
                opt.TemplateCharacterEnd = '>';
                opt.DefaultValueSeparator = Separator;
            });

        config["Svc:Url"].Should().Be("https://fallback.example.com");
    }

    [Fact]
    public void Default_SeparatorUnset_LeavesPlaceholderVerbatim()
    {
        var config = Build(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Svc:Value"] = "{Missing:-localhost}",
        });

        config["Svc:Value"].Should().Be("{Missing:-localhost}");
    }

    [Fact]
    public void Default_SeparatorUnset_KeyContainingSeparatorTextResolves()
    {
        var config = Build(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Svc:Value"] = "{Missing:-localhost}",
            ["Missing:-localhost"] = "literal key",
        });

        config["Svc:Value"].Should().Be("literal key");
    }

    private IConfiguration BuildWithSeparator(IDictionary<string, string?> data)
        => Build(data, opt => opt.DefaultValueSeparator = Separator);

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

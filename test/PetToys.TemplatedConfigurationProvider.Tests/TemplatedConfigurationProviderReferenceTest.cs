using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PetToys.TemplatedConfigurationProvider.Tests;

public sealed class TemplatedConfigurationProviderReferenceTest : IDisposable
{
    private static readonly Dictionary<string, string?> MemoryData = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ConnectionStrings:DbConnection"] = "Host=localhost;Password={ConnectionStrings:DbConnection:Password};",
        ["ConnectionStrings:DbConnection:Password"] = "Pa$Sw0{rD",

        ["AbsoluteReference1:OpenIdConnectOptions:Authority"] = "https://login.provider.com/{AbsoluteReference1:OpenIdConnectOptions:Authority:TenantId}/v2.0/",
        ["AbsoluteReference1:OpenIdConnectOptions:Authority:TenantId"] = "5A796309-2459-45E2-9255-FB328599839B",

        ["AbsoluteReference2:OpenIdConnectOptions:Authority"] = "https://login.provider.com/{OtherTenantId}/v2.0/",
        ["OtherTenantId"] = "5C193982-7822-4976-B20E-1FF96D4B332B",

        ["RelativeReference1:OpenIdConnectOptions:Authority"] = "https://login.provider.com/{OpenIdConnectOptions:Authority:TenantId}/v2.0/",
        ["RelativeReference1:OpenIdConnectOptions:Authority:TenantId"] = "C5356E88-1573-42B3-AAEE-E325FCA3F5D3",

        ["RelativeReference2:OpenIdConnectOptions:Authority"] = "https://login.provider.com/{Authority:TenantId}/v2.0/",
        ["RelativeReference2:OpenIdConnectOptions:Authority:TenantId"] = "B1587775-091D-4FBF-9331-7D6D2D0696C0",

        ["RelativeReference3:OpenIdConnectOptions:Authority"] = "https://login.provider.com/{TenantId}/v2.0/",
        ["RelativeReference3:OpenIdConnectOptions:Authority:TenantId"] = "CB2681E4-A5CB-4EEE-AD67-8CDCD41046E8",

        // Self-resolving relative placeholder: {Authority} resolves against the
        // value's own key, demonstrating single-pass (non-recursive) substitution.
        ["RelativeReference4:OpenIdConnectOptions:Authority"] = "https://login.provider.com/{Authority}/v2.0/",

        // Prefix-scoping decoy: a same-suffixed key under a DIFFERENT section
        // (RelativeReference6) must NOT satisfy a relative reference made from
        // RelativeReference5, so the placeholder is left verbatim.
        ["RelativeReference5:OpenIdConnectOptions:Authority"] = "https://login.provider.com/{OpenIdConnectOptions:Authority:TenantId}/v2.0/",
        ["RelativeReference6:OpenIdConnectOptions:Authority:TenantId"] = "F8E5CAF8-325E-4975-8A5A-C0494B5FCACB",

        // Two placeholders in one value, the first resolving to an empty string.
        ["AbsoluteReference3:TwoValues"] = "qwe{Replacements:EmptyValue}asd{Replacements:Value}zxc",
        ["Replacements:EmptyValue"] = "",
        ["Replacements:Value"] = "PJVr[6}Zr{yBz}GQ2U6Fj0My",
    };

    private readonly IConfigurationRoot _configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(MemoryData)
        .AddTemplatedConfiguration()
        .Build();

    [Fact]
    public void ConnectionString_WithAbsolutePlaceholder_ResolvesValue()
    {
        _configuration.GetConnectionString("DbConnection")
            .Should().Be("Host=localhost;Password=Pa$Sw0{rD;");
    }

    [Theory]
    [InlineData("AbsoluteReference1:OpenIdConnectOptions:Authority", "https://login.provider.com/5A796309-2459-45E2-9255-FB328599839B/v2.0/")]
    [InlineData("AbsoluteReference2:OpenIdConnectOptions:Authority", "https://login.provider.com/5C193982-7822-4976-B20E-1FF96D4B332B/v2.0/")]
    [InlineData("RelativeReference1:OpenIdConnectOptions:Authority", "https://login.provider.com/C5356E88-1573-42B3-AAEE-E325FCA3F5D3/v2.0/")]
    [InlineData("RelativeReference2:OpenIdConnectOptions:Authority", "https://login.provider.com/B1587775-091D-4FBF-9331-7D6D2D0696C0/v2.0/")]
    [InlineData("RelativeReference3:OpenIdConnectOptions:Authority", "https://login.provider.com/CB2681E4-A5CB-4EEE-AD67-8CDCD41046E8/v2.0/")]
    [InlineData("AbsoluteReference3:TwoValues", "qweasdPJVr[6}Zr{yBz}GQ2U6Fj0Myzxc")]
    public void Resolve_AbsoluteOrRelativeReference_ReturnsResolvedValue(string key, string expected)
    {
        _configuration.GetValue<string>(key).Should().Be(expected);
    }

    [Fact]
    public void Resolve_RelativePlaceholderMatchingOwnKey_SubstitutesRawValueOnce()
    {
        _configuration.GetValue<string>("RelativeReference4:OpenIdConnectOptions:Authority")
            .Should().Be("https://login.provider.com/https://login.provider.com/{Authority}/v2.0//v2.0/");
    }

    [Fact]
    public void Resolve_RelativePlaceholder_DoesNotMatchSameSuffixInOtherSection()
    {
        _configuration.GetValue<string>("RelativeReference5:OpenIdConnectOptions:Authority")
            .Should().Be("https://login.provider.com/{OpenIdConnectOptions:Authority:TenantId}/v2.0/");
    }

    public void Dispose() => ((IDisposable)_configuration).Dispose();
}

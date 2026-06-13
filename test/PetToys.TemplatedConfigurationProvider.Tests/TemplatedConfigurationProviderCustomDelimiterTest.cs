using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PetToys.TemplatedConfigurationProvider.Tests;

public sealed class TemplatedConfigurationProviderCustomDelimiterTest : IDisposable
{
    private static readonly Dictionary<string, string?> MemoryData = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Replacements:Value"] = "PJVr[6}Zr{yBz}GQ2U6Fj0My",
        ["Replacements:DifficultSituation4"] = "|||]]]}]]]]]|||||]]|[[[][][||]Replacements:Value|[]}}}}[{}}}[]}",
    };

    private readonly IConfigurationRoot _configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(MemoryData)
        .AddTemplatedConfiguration(opt =>
        {
            opt.TemplateCharacterStart = ']';
            opt.TemplateCharacterEnd = '|';
        })
        .Build();

    [Fact]
    public void Resolve_WithCustomDelimiters_SubstitutesValueAmidLiteralBraces()
    {
        _configuration.GetValue<string>("Replacements:DifficultSituation4")
            .Should().Be("|||]]]}]]]]]|||||]]|[[[][][||PJVr[6}Zr{yBz}GQ2U6Fj0My[]}}}}[{}}}[]}");
    }

    public void Dispose() => ((IDisposable)_configuration).Dispose();
}

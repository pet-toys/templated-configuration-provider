using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PetToys.TemplatedConfigurationProvider.Tests;

public sealed class TemplatedConfigurationProviderReplacementTest : IDisposable
{
    private static readonly Dictionary<string, string?> MemoryData = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Replacements:NullValue"] = null,
        ["Replacements:EmptyValue"] = "",
        ["Replacements:Value"] = "PJVr[6}Zr{yBz}GQ2U6Fj0My",

        ["Replacements:LeftPosition"] = "{Value} Lorem ipsum dolor sit",
        ["Replacements:RightPosition"] = "Lorem ipsum dolor sit {Value}",
        ["Replacements:InternalPosition"] = "Lorem ipsum {Value} dolor sit",
        ["Replacements:DifficultSituation1"] = "Lorem ipsum {NullValue} dolor sit",
        ["Replacements:DifficultSituation2"] = "Lorem ipsum {EmptyValue} dolor sit",
        ["Replacements:DifficultSituation3"] = "{}}{{}{{{{{}Lorem}}{{}{ipsum}}}}{{}}{}}}}{{{Value}{{dolor}}{{}}{}sit{{}{{{}",
    };

    private readonly IConfigurationRoot _configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(MemoryData)
        .AddTemplatedConfiguration()
        .Build();

    [Theory]
    [InlineData("Replacements:LeftPosition", "PJVr[6}Zr{yBz}GQ2U6Fj0My Lorem ipsum dolor sit")]
    [InlineData("Replacements:RightPosition", "Lorem ipsum dolor sit PJVr[6}Zr{yBz}GQ2U6Fj0My")]
    [InlineData("Replacements:InternalPosition", "Lorem ipsum PJVr[6}Zr{yBz}GQ2U6Fj0My dolor sit")]
    [InlineData("Replacements:DifficultSituation1", "Lorem ipsum  dolor sit")]
    [InlineData("Replacements:DifficultSituation2", "Lorem ipsum  dolor sit")]
    [InlineData("Replacements:DifficultSituation3", "{}}{{}{{{{{}Lorem}}{{}{ipsum}}}}{{}}{}}}}{{PJVr[6}Zr{yBz}GQ2U6Fj0My{{dolor}}{{}}{}sit{{}{{{}")]
    public void Resolve_PlaceholderRegardlessOfPosition_ReturnsResolvedValue(string key, string expected)
    {
        _configuration.GetValue<string>(key).Should().Be(expected);
    }

    public void Dispose() => ((IDisposable)_configuration).Dispose();
}

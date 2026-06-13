using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PetToys.TemplatedConfigurationProvider.Tests;

public sealed class TemplatedConfigurationOptionsValidationTest
{
    [Fact]
    public void AddTemplatedConfiguration_EqualCharacters_Throws()
    {
        var act = () => new ConfigurationBuilder()
            .AddTemplatedConfiguration(opt =>
            {
                opt.TemplateCharacterStart = '|';
                opt.TemplateCharacterEnd = '|';
            });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*must differ*");
    }

    [Theory]
    [InlineData(':', '}', "TemplateCharacterStart")]
    [InlineData('{', ':', "TemplateCharacterEnd")]
    public void AddTemplatedConfiguration_KeyDelimiterCharacter_Throws(char start, char end, string expectedParameter)
    {
        var act = () => new ConfigurationBuilder()
            .AddTemplatedConfiguration(opt =>
            {
                opt.TemplateCharacterStart = start;
                opt.TemplateCharacterEnd = end;
            });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*key delimiter*")
            .WithParameterName(expectedParameter);
    }

    [Theory]
    [InlineData(' ', '}', "TemplateCharacterStart")]
    [InlineData('{', '\t', "TemplateCharacterEnd")]
    [InlineData('\n', '}', "TemplateCharacterStart")]
    [InlineData('\0', '}', "TemplateCharacterStart")]
    public void AddTemplatedConfiguration_WhitespaceOrControlCharacter_Throws(char start, char end, string expectedParameter)
    {
        var act = () => new ConfigurationBuilder()
            .AddTemplatedConfiguration(opt =>
            {
                opt.TemplateCharacterStart = start;
                opt.TemplateCharacterEnd = end;
            });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*whitespace or control*")
            .WithParameterName(expectedParameter);
    }

    [Fact]
    public void AddTemplatedConfiguration_DistinctPrintableCharacters_RegistersAndResolves()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Svc:Url"] = "https://<Svc:Tenant>.example.com",
                ["Svc:Tenant"] = "acme",
            })
            .AddTemplatedConfiguration(opt =>
            {
                opt.TemplateCharacterStart = '<';
                opt.TemplateCharacterEnd = '>';
            })
            .Build();

        configuration["Svc:Url"].Should().Be("https://acme.example.com");
    }
}

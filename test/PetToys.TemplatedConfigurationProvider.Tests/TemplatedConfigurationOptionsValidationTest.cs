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
    [InlineData(':', '}')]
    [InlineData('{', ':')]
    public void AddTemplatedConfiguration_KeyDelimiterCharacter_Throws(char start, char end)
    {
        var act = () => new ConfigurationBuilder()
            .AddTemplatedConfiguration(opt =>
            {
                opt.TemplateCharacterStart = start;
                opt.TemplateCharacterEnd = end;
            });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*key delimiter*");
    }

    [Theory]
    [InlineData(' ', '}')]
    [InlineData('{', '\t')]
    [InlineData('\n', '}')]
    [InlineData('\0', '}')]
    public void AddTemplatedConfiguration_WhitespaceOrControlCharacter_Throws(char start, char end)
    {
        var act = () => new ConfigurationBuilder()
            .AddTemplatedConfiguration(opt =>
            {
                opt.TemplateCharacterStart = start;
                opt.TemplateCharacterEnd = end;
            });

        act.Should().Throw<ArgumentException>();
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

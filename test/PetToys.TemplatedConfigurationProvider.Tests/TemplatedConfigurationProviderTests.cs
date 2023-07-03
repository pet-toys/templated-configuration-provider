using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Xunit;

namespace PetToys.TemplatedConfigurationProvider.Tests
{
    public sealed class TemplatedConfigurationProviderTests
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

            ["RelativeReference4:OpenIdConnectOptions:Authority"] = "https://login.provider.com/{Authority}/v2.0/",

            ["RelativeReference5:OpenIdConnectOptions:Authority"] = "https://login.provider.com/{OpenIdConnectOptions:Authority:TenantId}/v2.0/",
            ["RelativeReference6:OpenIdConnectOptions:Authority:TenantId"] = "F8E5CAF8-325E-4975-8A5A-C0494B5FCACB",

            ["Replacements:NullValue"] = null,
            ["Replacements:EmptyValue"] = string.Empty,
            ["Replacements:Value"] = "PJVr[6}Zr{yBz}GQ2U6Fj0My",

            ["Replacements:LeftPosition"] = "{Value} Lorem ipsum dolor sit",
            ["Replacements:RightPosition"] = "Lorem ipsum dolor sit {Value}",
            ["Replacements:InternalPosition"] = "Lorem ipsum {Value} dolor sit",
            ["Replacements:DifficultSituation1"] = "Lorem ipsum {NullValue} dolor sit",
            ["Replacements:DifficultSituation2"] = "Lorem ipsum {EmptyValue} dolor sit",
            ["Replacements:DifficultSituation3"] = "{}}{{}{{{{{}Lorem}}{{}{ipsum}}}}{{}}{}}}}{{{Value}{{dolor}}{{}}{}sit{{}{{{}",

            ["Replacements:DifficultSituation4"] = "|||]]]}]]]]]|||||]]|[[[][][||]Replacements:Value|[]}}}}[{}}}[]}",
            ["Replacements:DifficultSituation5"] = "{L|(o)re^*m|Replacements:Value| gt)({[]][} ipsum dolor sit",
        };

        private readonly IConfigurationRoot _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(MemoryData)
            .AddTemplatedConfiguration()
            .Build();

        private readonly IConfigurationRoot _configurationWithOptionsModificator1 = new ConfigurationBuilder()
            .AddInMemoryCollection(MemoryData)
            .AddTemplatedConfiguration(opt =>
            {
                opt.TemplateCharacterStart = ']';
                opt.TemplateCharacterEnd = '|';
            })
            .Build();

        private readonly IConfigurationRoot _configurationWithOptionsModificator2 = new ConfigurationBuilder()
            .AddInMemoryCollection(MemoryData)
            .AddTemplatedConfiguration(opt =>
            {
                opt.TemplateCharacterStart = '|';
                opt.TemplateCharacterEnd = '|';
            })
            .Build();

        [Fact]
        public void TemplatedConfiguration_Providers()
        {
            var providers = _configuration.Providers.ToArray();
            providers.Length.Should().Be(2);
            providers[0].GetType().Should().Be<MemoryConfigurationProvider>();
            providers[1].GetType().Should().Be<TemplatedConfigurationProvider>();
        }

        [Fact]
        public void TemplatedConfiguration_SimpleOverride()
        {
            var connectionString = _configuration.GetConnectionString("DbConnection");
            connectionString.Should().Be("Host=localhost;Password=Pa$Sw0{rD;");
        }

        [Theory]
        [InlineData("AbsoluteReference1:OpenIdConnectOptions:Authority", "https://login.provider.com/5A796309-2459-45E2-9255-FB328599839B/v2.0/")]
        [InlineData("AbsoluteReference2:OpenIdConnectOptions:Authority", "https://login.provider.com/5C193982-7822-4976-B20E-1FF96D4B332B/v2.0/")]
        [InlineData("RelativeReference1:OpenIdConnectOptions:Authority", "https://login.provider.com/C5356E88-1573-42B3-AAEE-E325FCA3F5D3/v2.0/")]
        [InlineData("RelativeReference2:OpenIdConnectOptions:Authority", "https://login.provider.com/B1587775-091D-4FBF-9331-7D6D2D0696C0/v2.0/")]
        [InlineData("RelativeReference3:OpenIdConnectOptions:Authority", "https://login.provider.com/CB2681E4-A5CB-4EEE-AD67-8CDCD41046E8/v2.0/")]

        // edge case (meaningless)
        [InlineData("RelativeReference4:OpenIdConnectOptions:Authority", "https://login.provider.com/https://login.provider.com/{Authority}/v2.0//v2.0/")]
        public void TemplatedConfiguration_AbsoluteAndRelativeReferences(string key, string expected)
        {
            var value = _configuration.GetValue<string>(key);
            value.Should().Be(expected);
        }

        [Theory]
        [InlineData("RelativeReference5:OpenIdConnectOptions:Authority", "https://login.provider.com/{OpenIdConnectOptions:Authority:TenantId}/v2.0/")]
        public void TemplatedConfiguration_WrongRelativeReference(string key, string expected)
        {
            var value = _configuration.GetValue<string>(key);
            value.Should().Be(expected);
        }

        [Theory]
        [InlineData("Replacements:LeftPosition", "PJVr[6}Zr{yBz}GQ2U6Fj0My Lorem ipsum dolor sit")]
        [InlineData("Replacements:RightPosition", "Lorem ipsum dolor sit PJVr[6}Zr{yBz}GQ2U6Fj0My")]
        [InlineData("Replacements:InternalPosition", "Lorem ipsum PJVr[6}Zr{yBz}GQ2U6Fj0My dolor sit")]
        [InlineData("Replacements:DifficultSituation1", "Lorem ipsum  dolor sit")]
        [InlineData("Replacements:DifficultSituation2", "Lorem ipsum  dolor sit")]
        [InlineData("Replacements:DifficultSituation3", "{}}{{}{{{{{}Lorem}}{{}{ipsum}}}}{{}}{}}}}{{PJVr[6}Zr{yBz}GQ2U6Fj0My{{dolor}}{{}}{}sit{{}{{{}")]
        public void TemplatedConfiguration_ReplacementOptions(string key, string expected)
        {
            var value = _configuration.GetValue<string>(key);
            value.Should().Be(expected);
        }

        [Theory]
        [InlineData("Replacements:DifficultSituation4", "|||]]]}]]]]]|||||]]|[[[][][||PJVr[6}Zr{yBz}GQ2U6Fj0My[]}}}}[{}}}[]}")]
        public void TemplatedConfiguration_OptionsModificator1(string key, string expected)
        {
            var value = _configurationWithOptionsModificator1.GetValue<string>(key);
            value.Should().Be(expected);
        }

        [Theory]
        [InlineData("Replacements:DifficultSituation5", "{L|(o)re^*mPJVr[6}Zr{yBz}GQ2U6Fj0My gt)({[]][} ipsum dolor sit")]
        public void TemplatedConfiguration_OptionsModificator2(string key, string expected)
        {
            var value = _configurationWithOptionsModificator2.GetValue<string>(key);
            value.Should().Be(expected);
        }
    }
}

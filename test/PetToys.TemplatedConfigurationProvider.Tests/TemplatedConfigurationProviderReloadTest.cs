using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PetToys.TemplatedConfigurationProvider.Tests;

public sealed class TemplatedConfigurationProviderReloadTest
{
#if OS_MAC
    private const int Timeout = 7000;
#else
    private const int Timeout = 700;
#endif

    private const string Json1 = """
                                 {
                                   "ConnectionStrings": {
                                     "DbConnection": "Host=localhost;Password={DbConnection:Password};",
                                     "DbConnection:Password": "Pa$Sw0{rD"
                                   }
                                 }
                                 """;

    private const string Json2 = """
                                 {
                                   "ConnectionStrings": {
                                     "DbConnection": "Host=localhost;Password={DbConnection:Password};",
                                     "DbConnection:Password": "Pa$S}w0rD"
                                   }
                                 }
                                 """;

    [Theory]
    [InlineData("Host=localhost;Password=Pa$Sw0{rD;", "Host=localhost;Password=Pa$S}w0rD;")]
    public async Task ReloadTest(string expected1, string expected2)
    {
        var fileName = await WriteToTempFileAsync(Json1);
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(fileName, optional: false, reloadOnChange: true)
            .AddTemplatedConfiguration()
            .Build();

        var result = configuration.GetConnectionString("DbConnection");
        result.Should().Be(expected1);

        await WriteToTempFileAsync(Json2, fileName);
        await Task.Delay(Timeout);
        result = configuration.GetConnectionString("DbConnection");
        result.Should().Be(expected2);

        await WriteToTempFileAsync(Json1, fileName);
        await Task.Delay(Timeout);
        result = configuration.GetConnectionString("DbConnection");
        result.Should().Be(expected1);

        File.Delete(fileName);
    }

    private static async Task<string> WriteToTempFileAsync(string content, string? fileName = null)
    {
        fileName ??= Path.GetTempFileName();
        await using var writer = new StreamWriter(fileName);
        await writer.WriteAsync(content);
        return fileName;
    }
}

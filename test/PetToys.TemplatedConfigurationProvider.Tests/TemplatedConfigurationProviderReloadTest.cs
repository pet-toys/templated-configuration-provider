using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PetToys.TemplatedConfigurationProvider.Tests;

public sealed class TemplatedConfigurationProviderReloadTest
{
    private const int Timeout = 10_000;

    private const string Json1 = """
                                 {
                                   "ConnectionStrings": {
                                     "DbConnection1": "Host=localhost;Password={DbConnection1:Password};",
                                     "DbConnection1:Password": "Pa$Sw0{rD",
                                     "DbConnection2": "Host=localhost;Password={DbConnection2:Password};",
                                     "DbConnection2:Password": "$SPaw0{rD"
                                   }
                                 }
                                 """;

    private const string Json2 = """
                                 {
                                   "ConnectionStrings": {
                                     "DbConnection1": "Host=localhost;Password={DbConnection1:Password};",
                                     "DbConnection1:Password": "Pa$S}w0rD"
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

        var result = configuration.GetConnectionString("DbConnection1");
        result.Should().Be(expected1);

        await WriteToTempFileAsync(Json2, fileName);
        await Task.Delay(Timeout, TestContext.Current.CancellationToken);
        result = configuration.GetConnectionString("DbConnection1");
        result.Should().Be(expected2);

        await WriteToTempFileAsync(Json1, fileName);
        await Task.Delay(Timeout, TestContext.Current.CancellationToken);
        result = configuration.GetConnectionString("DbConnection1");
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

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PetToys.TemplatedConfigurationProvider.Tests;

/// <summary>
/// End-to-end smoke test that the provider re-templates when an underlying
/// <c>reloadOnChange</c> file source changes on disk. Deterministic reload
/// semantics (update / remove / add / no-op) live in
/// <see cref="TemplatedConfigurationProviderReloadSemanticsTest"/>; this test
/// only verifies the real file-watcher wiring, polling for the change instead
/// of sleeping for a fixed interval.
/// </summary>
public sealed class TemplatedConfigurationProviderReloadTest
{
    private const int PollIntervalMs = 25;
    private const int TimeoutMs = 10_000;

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
        var ct = TestContext.Current.CancellationToken;
        var fileName = Path.GetTempFileName();
        try
        {
            await WriteToFileAsync(fileName, Json1, ct);
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(fileName, optional: false, reloadOnChange: true)
                .AddTemplatedConfiguration()
                .Build();

            configuration.GetConnectionString("DbConnection1").Should().Be(expected1);

            await WriteToFileAsync(fileName, Json2, ct);
            await WaitForConnectionStringAsync(configuration, "DbConnection1", expected2, ct);

            await WriteToFileAsync(fileName, Json1, ct);
            await WaitForConnectionStringAsync(configuration, "DbConnection1", expected1, ct);
        }
        finally
        {
            File.Delete(fileName);
        }
    }

    private static async Task WaitForConnectionStringAsync(
        IConfiguration configuration,
        string name,
        string expected,
        CancellationToken ct)
    {
        var elapsed = 0;
        while (configuration.GetConnectionString(name) != expected && elapsed < TimeoutMs)
        {
            await Task.Delay(PollIntervalMs, ct);
            elapsed += PollIntervalMs;
        }

        configuration.GetConnectionString(name).Should().Be(expected);
    }

    private static async Task WriteToFileAsync(string fileName, string content, CancellationToken ct)
    {
        await using var writer = new StreamWriter(fileName);
        await writer.WriteAsync(content.AsMemory(), ct);
    }
}

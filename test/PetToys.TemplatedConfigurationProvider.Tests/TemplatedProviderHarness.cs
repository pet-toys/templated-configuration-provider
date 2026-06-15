using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace PetToys.TemplatedConfigurationProvider.Tests;

/// <summary>
/// Wires a <see cref="TemplatedConfigurationProvider"/> over a single
/// <see cref="TriggerableConfigurationSource"/> and performs the initial
/// <c>Load</c>, so tests can drive the provider directly without repeating the
/// builder boilerplate. Dispose to release the provider's inner root and
/// change-token registration.
/// </summary>
internal sealed class TemplatedProviderHarness : IDisposable
{
    public TemplatedProviderHarness(
        IDictionary<string, string?> initialData,
        Action<TemplatedConfigurationOptions>? configure = null)
    {
        Source = new TriggerableConfigurationSource(initialData);

        var options = new TemplatedConfigurationOptions();
        configure?.Invoke(options);

        var builder = new ConfigurationBuilder();
        builder.Sources.Add(Source);

        var templatedSource = new TemplatedConfigurationSource(options);
        builder.Sources.Add(templatedSource);

        Provider = new TemplatedConfigurationProvider(options, builder, templatedSource);
        Provider.Load();
    }

    public TriggerableConfigurationSource Source { get; }

    public TemplatedConfigurationProvider Provider { get; }

    public void Dispose() => Provider.Dispose();
}

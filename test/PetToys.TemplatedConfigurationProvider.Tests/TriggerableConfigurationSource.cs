using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace PetToys.TemplatedConfigurationProvider.Tests;

/// <summary>
/// A test configuration source backed by a mutable dictionary. It lets a test
/// mutate the underlying data either silently (<see cref="SetQuiet"/>,
/// <see cref="RemoveQuiet"/>) or with a reload notification (<see cref="SetValue"/>),
/// so both the explicit <c>Load</c> path and the change-token path can be driven
/// deterministically without touching the file system.
/// </summary>
internal sealed class TriggerableConfigurationSource(IDictionary<string, string?> initialData) : IConfigurationSource
{
    private TriggerableProvider? _provider;

    /// <summary>Changes a value and fires a reload notification.</summary>
    public void SetValue(string key, string? value) => _provider?.SetValue(key, value);

    /// <summary>Removes a key and fires a reload notification.</summary>
    public void Remove(string key) => _provider?.Remove(key);

    /// <summary>Changes a value without firing a reload notification.</summary>
    public void SetQuiet(string key, string? value) => _provider?.SetQuiet(key, value);

    /// <summary>Removes a key without firing a reload notification.</summary>
    public void RemoveQuiet(string key) => _provider?.RemoveQuiet(key);

    public IConfigurationProvider Build(IConfigurationBuilder builder)
        => _provider = new TriggerableProvider(initialData);

    private sealed class TriggerableProvider(IDictionary<string, string?> initialData) : ConfigurationProvider
    {
        public override void Load()
            => Data = new Dictionary<string, string?>(initialData, StringComparer.OrdinalIgnoreCase);

        public void SetValue(string key, string? value)
        {
            Data[key] = value;
            OnReload();
        }

        public void Remove(string key)
        {
            Data.Remove(key);
            OnReload();
        }

        public void SetQuiet(string key, string? value) => Data[key] = value;

        public void RemoveQuiet(string key) => Data.Remove(key);
    }
}

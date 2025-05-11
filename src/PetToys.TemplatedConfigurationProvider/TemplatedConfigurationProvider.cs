using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace PetToys.TemplatedConfigurationProvider;

internal sealed class TemplatedConfigurationProvider
    : ConfigurationProvider, IDisposable
{
    private readonly char _startChar;
    private readonly char _endChar;
    private readonly ConfigurationRoot _root;
    private readonly IDisposable _changeTokenRegistration;

    public TemplatedConfigurationProvider(TemplatedConfigurationOptions options, IConfigurationBuilder builder)
    {
        _startChar = options.TemplateCharacterStart;
        _endChar = options.TemplateCharacterEnd;
        var otherProviders = builder.Sources
            .Where(s => s.GetType() != typeof(TemplatedConfigurationSource))
            .Select(source => source.Build(builder))
            .ToList();
        _root = new ConfigurationRoot(otherProviders);
        _changeTokenRegistration = ChangeToken.OnChange(_root.GetReloadToken, Reload);
    }

    public override void Load()
    {
        foreach (var (key, value) in GetInnerData())
        {
            Data[key] = value;
        }
    }

    private void Reload()
    {
        var changed = false;
        var ownData = new Dictionary<string, string?>(GetInnerData(), StringComparer.OrdinalIgnoreCase);
        var deleted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, oldValue) in Data)
        {
            if (ownData.TryGetValue(key, out var value))
            {
                if (value == oldValue) continue;
                Data[key] = value;
                changed = true;
                continue;
            }

            deleted.Add(key);
            changed = true;
        }

        foreach (var key in deleted)
        {
            Data.Remove(key);
        }

        if (changed) OnReload();
    }

    private IEnumerable<KeyValuePair<string, string?>> GetInnerData()
    {
        var otherData = new Dictionary<string, string?>(
            _root.AsEnumerable(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in otherData.Where(kv => kv.Value is not null))
        {
            if (FoundReplacement(otherData, key, value!, out var replacement))
            {
                yield return new KeyValuePair<string, string?>(key, replacement);
            }
        }
    }

    private bool FoundReplacement(IDictionary<string, string?> data, string originalKey, string value, [MaybeNullWhen(false)] out string replacement)
    {
        replacement = null;
        var startIndexes = AllIndexesOf(_startChar, value).Reverse().ToArray();
        var endIndexes = AllIndexesOf(_endChar, value).ToArray();
        if (startIndexes.Length == 0 || endIndexes.Length == 0) return false;

        foreach (var startIndex in startIndexes)
        {
            foreach (var endIndex in endIndexes.Where(i => i > startIndex))
            {
                if (!FoundValue(data, originalKey, value[(startIndex + 1)..endIndex], out var newValue))
                    continue;

                replacement = value[..startIndex] + newValue + value[(endIndex + 1)..];
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<int> AllIndexesOf(char symbol, string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == symbol) yield return i;
        }
    }

    private static bool FoundValue(IDictionary<string, string?> data, string originalKey, string key, out string? value)
    {
        value = string.Empty;
        var segments = new List<string> { string.Empty };
        foreach (var fragment in originalKey.Split(ConfigurationPath.KeyDelimiter))
        {
            segments.Add(segments[^1] + fragment + ConfigurationPath.KeyDelimiter);
        }

        foreach (var segment in segments)
        {
            if (data.TryGetValue(segment + key, out value))
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        _root.Dispose();
        _changeTokenRegistration.Dispose();
    }
}

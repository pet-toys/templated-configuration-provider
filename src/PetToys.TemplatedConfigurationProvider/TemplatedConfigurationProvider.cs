using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace PetToys.TemplatedConfigurationProvider;

internal sealed class TemplatedConfigurationProvider
    : ConfigurationProvider, IDisposable
{
    private readonly char _startChar;
    private readonly char _endChar;
    private readonly bool _throwOnUnresolvedPlaceholders;
    private readonly ConfigurationRoot _root;
    private readonly IDisposable _changeTokenRegistration;
    private bool _disposed;

    public TemplatedConfigurationProvider(TemplatedConfigurationOptions options, IConfigurationBuilder builder)
    {
        _startChar = options.TemplateCharacterStart;
        _endChar = options.TemplateCharacterEnd;
        _throwOnUnresolvedPlaceholders = options.ThrowOnUnresolvedPlaceholders;
        var otherProviders = builder.Sources
            .Where(s => s.GetType() != typeof(TemplatedConfigurationSource))
            .Select(source => source.Build(builder))
            .ToList();
        _root = new ConfigurationRoot(otherProviders);
        _changeTokenRegistration = ChangeToken.OnChange(_root.GetReloadToken, Reload);
    }

    public override void Load()
    {
        Data = BuildTemplatedData();
    }

    private void Reload()
    {
        var newData = BuildTemplatedData();
        if (DataEquals(Data, newData))
        {
            return;
        }

        Data = newData;
        OnReload();
    }

    private Dictionary<string, string?> BuildTemplatedData()
        => new(GetInnerData(), StringComparer.OrdinalIgnoreCase);

    private static bool DataEquals(
        IDictionary<string, string?> current,
        Dictionary<string, string?> candidate)
    {
        if (current.Count != candidate.Count)
        {
            return false;
        }

        foreach (var (key, value) in candidate)
        {
            if (!current.TryGetValue(key, out var existing)
                || !string.Equals(existing, value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private IEnumerable<KeyValuePair<string, string?>> GetInnerData()
    {
        var otherData = new Dictionary<string, string?>(
            _root.AsEnumerable(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in otherData.Where(kv => kv.Value is not null))
        {
            if (TryReplace(otherData, key, value!, out var replacement))
            {
                yield return new KeyValuePair<string, string?>(key, replacement);
            }
        }
    }

    private bool TryReplace(IDictionary<string, string?> data, string originalKey, string value, [MaybeNullWhen(false)] out string replacement)
    {
        if (!value.Contains(_startChar) || !value.Contains(_endChar))
        {
            replacement = null;
            return false;
        }

        var sb = new StringBuilder();
        var i = 0;
        var anyReplaced = false;

        while (i < value.Length)
        {
            var start = value.IndexOf(_startChar, i);
            if (start == -1)
            {
                sb.Append(value, i, value.Length - i);
                break;
            }

            var keyStart = start + 1;
            var searchFrom = keyStart;
            var matched = false;
            string? unresolvedKey = null;

            while (true)
            {
                var end = value.IndexOf(_endChar, searchFrom);
                if (end == -1) break;

                var key = value[keyStart..end];
                unresolvedKey ??= key;
                if (TryGetReplacement(data, originalKey, key, out var rep))
                {
                    sb.Append(value, i, start - i);
                    sb.Append(rep);
                    i = end + 1;
                    anyReplaced = true;
                    matched = true;
                    break;
                }

                searchFrom = end + 1;
            }

            if (matched) continue;

            if (_throwOnUnresolvedPlaceholders && unresolvedKey is not null)
            {
                throw new InvalidOperationException(
                    $"Configuration key '{originalKey}' contains unresolved placeholder '{unresolvedKey}'.");
            }

            sb.Append(value, i, start - i);
            sb.Append(_startChar);
            i = keyStart;
        }

        if (anyReplaced)
        {
            replacement = sb.ToString();
            return true;
        }

        replacement = null;
        return false;
    }

    private static bool TryGetReplacement(IDictionary<string, string?> data, string originalKey, string key, [MaybeNullWhen(false)] out string value)
    {
        value = null;
        var segments = new List<string> { string.Empty };
        foreach (var fragment in originalKey.Split(ConfigurationPath.KeyDelimiter))
        {
            segments.Add(segments[^1] + fragment + ConfigurationPath.KeyDelimiter);
        }

        foreach (var segment in segments)
        {
            if (!data.TryGetValue(segment + key, out var val)) continue;
            value = val ?? string.Empty;
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _changeTokenRegistration.Dispose();
        _root.Dispose();
    }
}

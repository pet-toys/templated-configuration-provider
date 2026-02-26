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

        StringBuilder? sb = null;
        var pendingStart = 0;
        var i = 0;

        while (i < value.Length)
        {
            var start = value.IndexOf(_startChar, i);
            if (start == -1)
            {
                if (sb != null)
                    sb.Append(value, pendingStart, value.Length - pendingStart);
                break;
            }

            var keyStart = start + 1;
            var searchFrom = keyStart;
            var matched = false;

            while (true)
            {
                var end = value.IndexOf(_endChar, searchFrom);
                if (end == -1) break;

                var key = value[keyStart..end];
                if (TryGetReplacement(data, originalKey, key, out var rep))
                {
                    sb ??= new StringBuilder(value.Length);
                    sb.Append(value, pendingStart, start - pendingStart);
                    sb.Append(rep);
                    pendingStart = end + 1;
                    i = end + 1;
                    matched = true;
                    break;
                }

                searchFrom = end + 1;
            }

            if (matched) continue;

            i = keyStart;
        }

        if (sb != null)
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
        _root.Dispose();
        _changeTokenRegistration.Dispose();
    }
}

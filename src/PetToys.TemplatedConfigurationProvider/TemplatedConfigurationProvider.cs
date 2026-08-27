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

    public TemplatedConfigurationProvider(
        TemplatedConfigurationOptions options,
        IConfigurationBuilder builder,
        TemplatedConfigurationSource source)
    {
        _startChar = options.TemplateCharacterStart;
        _endChar = options.TemplateCharacterEnd;
        _throwOnUnresolvedPlaceholders = options.ThrowOnUnresolvedPlaceholders;
        var otherProviders = builder.Sources
            .TakeWhile(s => !ReferenceEquals(s, source))
            .Where(s => s.GetType() != typeof(TemplatedConfigurationSource))
            .Select(s => s.Build(builder))
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
        Dictionary<string, string?> newData;
        try
        {
            newData = BuildTemplatedData();
        }
        catch (InvalidOperationException) when (_throwOnUnresolvedPlaceholders)
        {
            return;
        }

        if (DataEquals(Data, newData))
        {
            return;
        }

        Data = newData;
        OnReload();
    }

    /// <summary>
    /// Snapshots the sources this provider resolves against and returns the
    /// subset of their keys whose value contains at least one placeholder that
    /// could be replaced.
    /// </summary>
    private Dictionary<string, string?> BuildTemplatedData()
    {
        var sourceData = new Dictionary<string, string?>(
            _root.AsEnumerable(),
            StringComparer.OrdinalIgnoreCase);
        var templatedData = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in sourceData)
        {
            if (value is not null && TryReplace(sourceData, key, value, out var replacement))
            {
                templatedData[key] = replacement;
            }
        }

        return templatedData;
    }

    // 'candidate' stays concrete: CA1859 rejects an interface parameter that is
    // only ever handed a Dictionary, and 'current' cannot follow it because
    // ConfigurationProvider.Data is typed as IDictionary.
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

    /// <summary>
    /// Replaces every resolvable placeholder in <paramref name="value"/>,
    /// returning <see langword="false"/> when nothing was replaced so the
    /// caller can leave the key to its original provider.
    /// </summary>
    private bool TryReplace(
        IReadOnlyDictionary<string, string?> data,
        string originalKey,
        string value,
        [MaybeNullWhen(false)] out string replacement)
    {
        replacement = null;

        if (!value.Contains(_startChar) || !value.Contains(_endChar))
        {
            return false;
        }

        var lookupPrefixes = GetLookupPrefixes(originalKey);
        var builder = new StringBuilder(value.Length);
        var position = 0;
        var anyReplaced = false;

        while (position < value.Length)
        {
            var start = value.IndexOf(_startChar, position);
            if (start == -1)
            {
                builder.Append(value, position, value.Length - position);
                break;
            }

            builder.Append(value, position, start - position);

            if (TryResolvePlaceholder(data, lookupPrefixes, value, start, out var resolved, out var resumeAt))
            {
                builder.Append(resolved);
                position = resumeAt;
                anyReplaced = true;
                continue;
            }

            ThrowIfStrict(value, originalKey, start);

            // Not a placeholder after all: emit the delimiter and keep scanning
            // from the character after it, so a nested start delimiter still
            // gets its own chance to resolve.
            builder.Append(_startChar);
            position = start + 1;
        }

        if (!anyReplaced)
        {
            return false;
        }

        replacement = builder.ToString();
        return true;
    }

    /// <summary>
    /// Tries every end delimiter after <paramref name="start"/> in turn,
    /// resolving the first candidate body that names a known key; a body that
    /// contains an end delimiter therefore still resolves when the longer
    /// reading is the one that matches.
    /// </summary>
    private bool TryResolvePlaceholder(
        IReadOnlyDictionary<string, string?> data,
        List<string> lookupPrefixes,
        string value,
        int start,
        [MaybeNullWhen(false)] out string resolved,
        out int resumeAt)
    {
        var bodyStart = start + 1;
        var searchFrom = bodyStart;

        while (true)
        {
            var end = value.IndexOf(_endChar, searchFrom);
            if (end == -1)
            {
                resolved = null;
                resumeAt = bodyStart;
                return false;
            }

            if (TryLookup(data, lookupPrefixes, value[bodyStart..end], out resolved))
            {
                resumeAt = end + 1;
                return true;
            }

            searchFrom = end + 1;
        }
    }

    /// <summary>
    /// Resolves <paramref name="key"/> against the snapshot, trying the root
    /// first and then each section of the value's own key, so the first match
    /// wins.
    /// </summary>
    private static bool TryLookup(
        IReadOnlyDictionary<string, string?> data,
        List<string> lookupPrefixes,
        string key,
        [MaybeNullWhen(false)] out string value)
    {
        foreach (var prefix in lookupPrefixes)
        {
            if (data.TryGetValue(prefix + key, out var found))
            {
                value = found ?? string.Empty;
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Builds the lookup prefixes for a key -- the empty root prefix followed
    /// by each of its sections -- once per value instead of once per candidate.
    /// </summary>
    private static List<string> GetLookupPrefixes(string originalKey)
    {
        var prefixes = new List<string> { string.Empty };
        foreach (var fragment in originalKey.Split(ConfigurationPath.KeyDelimiter))
        {
            prefixes.Add(prefixes[^1] + fragment + ConfigurationPath.KeyDelimiter);
        }

        return prefixes;
    }

    private void ThrowIfStrict(string value, string originalKey, int start)
    {
        if (!_throwOnUnresolvedPlaceholders)
        {
            return;
        }

        var bodyStart = start + 1;
        var end = value.IndexOf(_endChar, bodyStart);
        if (end == -1)
        {
            // Unbalanced, not unresolved: it passes through verbatim.
            return;
        }

        throw new InvalidOperationException(
            $"Configuration key '{originalKey}' contains unresolved placeholder '{value[bodyStart..end]}'.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _changeTokenRegistration.Dispose();
        _root.Dispose();
    }
}

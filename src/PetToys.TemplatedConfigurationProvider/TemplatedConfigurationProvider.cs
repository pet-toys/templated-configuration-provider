#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace PetToys.TemplatedConfigurationProvider
{
    internal sealed class TemplatedConfigurationProvider : ConfigurationProvider
    {
        private readonly char _startChar;
        private readonly char _endChar;
        private readonly IConfigurationRoot _configurationRoot;

        private Dictionary<string, string?> _otherProvidersData = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        public TemplatedConfigurationProvider(
            TemplatedConfigurationOptions options,
            IConfigurationBuilder builder)
        {
            _startChar = options.TemplateCharacterStart;
            _endChar = options.TemplateCharacterEnd;

            var providers = builder.Sources
                .Where(s => s.GetType() != typeof(TemplatedConfigurationSource))
                .Select(source => source.Build(builder))
                .ToList();
            _configurationRoot = new ConfigurationRoot(providers);
        }

        public override void Load()
        {
            Data.Clear();
            _otherProvidersData = new Dictionary<string, string?>(_configurationRoot.AsEnumerable(), StringComparer.OrdinalIgnoreCase);

            foreach (var kv in _otherProvidersData.Where(kv => kv.Value is not null))
            {
                if (FoundReplacement(kv.Key, kv.Value!, out var replacement))
                {
                    Data[kv.Key] = replacement;
                }
            }
        }

        private static IEnumerable<int> AllIndexesOf(char symbol, string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] == symbol) yield return i;
            }
        }

        private bool FoundReplacement(string originalKey, string value, out string replacement)
        {
            replacement = string.Empty;
            var startIndexes = AllIndexesOf(_startChar, value).Reverse().ToArray();
            var endIndexes = AllIndexesOf(_endChar, value).ToArray();
            if (startIndexes.Length == 0 || endIndexes.Length == 0) return false;

            foreach (var startIndex in startIndexes)
            {
                foreach (var endIndex in endIndexes.Where(i => i > startIndex))
                {
                    if (!FoundValue(originalKey, value[(startIndex + 1)..endIndex], out var newValue))
                        continue;

                    replacement = value[..startIndex] + newValue + value[(endIndex + 1)..];
                    return true;
                }
            }

            return false;
        }

        private bool FoundValue(string originalKey, string key, out string? value)
        {
            value = string.Empty;
            var segments = new List<string> { string.Empty };
            foreach (var fragment in originalKey.Split(ConfigurationPath.KeyDelimiter))
            {
                segments.Add(segments.Last() + fragment + ConfigurationPath.KeyDelimiter);
            }

            foreach (var segment in segments)
            {
                if (_otherProvidersData.TryGetValue(segment + key, out value))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

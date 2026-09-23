using System;
using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Infrastructure.Storage;

namespace Flow.Launcher.SearchFilters;

public sealed class CustomExtensionSettings
{
    public List<string> Extensions { get; set; } = [];
}

internal sealed class QueryFilterCustomExtensions
{
    private readonly JsonStorage<CustomExtensionSettings> _storage;
    private readonly CustomExtensionSettings _settings;

    internal QueryFilterCustomExtensions(JsonStorage<CustomExtensionSettings> storage = null)
    {
        _storage = storage ?? new FlowLauncherJsonStorage<CustomExtensionSettings>();
        _settings = _storage.Load();
    }

    internal IReadOnlyList<string> Values => QueryFilterExtensionValue.Union(_settings.Extensions)
        .Except(QueryFilterCatalog.ExtensionPresets, StringComparer.OrdinalIgnoreCase).ToArray();

    internal void Add(string value)
    {
        if (!QueryFilterExtensionValue.TryNormalizeOne(value, out var extension)
            || QueryFilterCatalog.ExtensionPresets.Contains(extension)
            || Values.Contains(extension))
        {
            return;
        }

        _settings.Extensions = [.. Values, extension];
        _storage.Save();
    }

    internal void Remove(string value)
    {
        _settings.Extensions = Values.Where(item => !item.Equals(value, StringComparison.OrdinalIgnoreCase)).ToList();
        _storage.Save();
    }
}

namespace Flow.Launcher.SearchFilters;

internal static class QueryFilterLabels
{
    internal static string Chip(QueryFilterId id) => id switch
    {
        QueryFilterId.File => Localize.searchFilter_file(),
        QueryFilterId.Folder => Localize.searchFilter_folder(),
        QueryFilterId.Path => Localize.searchFilter_path(),
        QueryFilterId.Extension => Localize.searchFilter_ext(),
        QueryFilterId.Size => Localize.searchFilter_size(),
        QueryFilterId.DateModified => Localize.searchFilter_modified(),
        QueryFilterId.DateCreated => Localize.searchFilter_created(),
        QueryFilterId.DateAccessed => Localize.searchFilter_accessed(),
        QueryFilterId.Hidden => Localize.searchFilter_hidden(),
        QueryFilterId.NameMatch => Localize.searchFilter_nameMatch(),
        QueryFilterId.CaseSensitive => Localize.searchFilter_caseSensitive(),
        _ => id.ToString()
    };

    internal static string Display(QueryFilterId id, string label, string value, bool isSelected)
    {
        if (!isSelected || string.IsNullOrEmpty(value) || !QueryFilterCatalog.RequiresValue(id))
        {
            return label;
        }

        return $"{label}: {Preset(id, value)}";
    }

    internal static string Tooltip(QueryFilterId id, string value)
    {
        if (id == QueryFilterId.Path && string.IsNullOrEmpty(value))
        {
            return Localize.searchFilter_tooltip(@"C:\folder\>");
        }

        if (id == QueryFilterId.Extension && string.IsNullOrEmpty(value))
        {
            return Localize.searchFilter_tooltip("ext:png;jpg");
        }

        if (id == QueryFilterId.NameMatch && string.IsNullOrEmpty(value))
        {
            return Localize.searchFilter_tooltip("match:exact");
        }

        var syntax = QueryFilterCatalog.RequiresValue(id) && !string.IsNullOrEmpty(value)
            ? QueryFilterCatalog.Format(id, value)
            : QueryFilterCatalog.Format(id, QueryFilterCatalog.RequiresValue(id) ? "…" : string.Empty);

        return Localize.searchFilter_tooltip(syntax);
    }

    internal static string Preset(QueryFilterId id, string value)
    {
        if (id == QueryFilterId.Size)
        {
            return SizeLabel(value);
        }

        if (id is QueryFilterId.DateModified or QueryFilterId.DateCreated or QueryFilterId.DateAccessed)
        {
            return DateLabel(value);
        }

        if (id == QueryFilterId.Extension)
        {
            return QueryFilterExtensionValue.ToDisplay(value);
        }

        if (id == QueryFilterId.Path)
        {
            return QueryFilterPathValue.ToDisplay(value);
        }

        if (id == QueryFilterId.NameMatch)
        {
            return value.ToLowerInvariant() switch
            {
                "exact" => Localize.searchFilter_match_exact(),
                "prefix" => Localize.searchFilter_match_prefix(),
                "suffix" => Localize.searchFilter_match_suffix(),
                "word" => Localize.searchFilter_match_word(),
                _ => value
            };
        }

        return value;
    }

    private static string SizeLabel(string value) => value.ToLowerInvariant() switch
    {
        "empty" => Localize.searchFilter_size_empty(),
        "tiny" => Localize.searchFilter_size_tiny(),
        "small" => Localize.searchFilter_size_small(),
        "medium" => Localize.searchFilter_size_medium(),
        "large" => Localize.searchFilter_size_large(),
        "huge" => Localize.searchFilter_size_huge(),
        "gigantic" => Localize.searchFilter_size_gigantic(),
        _ => QueryFilterSizeValue.ToDisplay(value)
    };

    private static string DateLabel(string value) => value.ToLowerInvariant() switch
    {
        "today" => Localize.searchFilter_date_today(),
        "yesterday" => Localize.searchFilter_date_yesterday(),
        "thisweek" => Localize.searchFilter_date_thisweek(),
        "lastweek" => Localize.searchFilter_date_lastweek(),
        "thismonth" => Localize.searchFilter_date_thismonth(),
        "lastmonth" => Localize.searchFilter_date_lastmonth(),
        "thisyear" => Localize.searchFilter_date_thisyear(),
        "lastyear" => Localize.searchFilter_date_lastyear(),
        _ => value
    };

    internal static string ExtensionGroup(string key) => key switch
    {
        "image" => Localize.searchFilter_image(),
        "video" => Localize.searchFilter_video(),
        "audio" => Localize.searchFilter_audio(),
        "document" => Localize.searchFilter_document(),
        "archive" => Localize.searchFilter_archive(),
        "exe" => Localize.searchFilter_exe(),
        _ => key
    };
}

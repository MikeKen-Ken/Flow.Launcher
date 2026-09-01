namespace Flow.Launcher.SearchFilters;

internal static class QueryFilterLabels
{
    internal static string Chip(QueryFilterId id) => id switch
    {
        QueryFilterId.File => Localize.searchFilter_file(),
        QueryFilterId.Folder => Localize.searchFilter_folder(),
        QueryFilterId.Path => Localize.searchFilter_path(),
        QueryFilterId.Image => Localize.searchFilter_image(),
        QueryFilterId.Video => Localize.searchFilter_video(),
        QueryFilterId.Audio => Localize.searchFilter_audio(),
        QueryFilterId.Document => Localize.searchFilter_document(),
        QueryFilterId.Archive => Localize.searchFilter_archive(),
        QueryFilterId.Executable => Localize.searchFilter_exe(),
        QueryFilterId.Extension => Localize.searchFilter_ext(),
        QueryFilterId.Size => Localize.searchFilter_size(),
        QueryFilterId.DateModified => Localize.searchFilter_modified(),
        QueryFilterId.DateCreated => Localize.searchFilter_created(),
        QueryFilterId.DateAccessed => Localize.searchFilter_accessed(),
        QueryFilterId.Hidden => Localize.searchFilter_hidden(),
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
            return Localize.searchFilter_tooltip("path:\"…\"");
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
            return value.TrimStart('.').ToLowerInvariant();
        }

        if (id == QueryFilterId.Path)
        {
            return QueryFilterPathValue.ToDisplay(value);
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
}

namespace Flow.Launcher.SearchFilters;

internal enum QueryFilterId
{
    File,
    Folder,
    Path,
    Size,
    DateModified,
    DateCreated,
    Extension,
    DateAccessed,
    Hidden
}

internal enum QueryFilterGroup
{
    Kind,
    Path,
    Size,
    DateModified,
    DateCreated,
    Extension,
    DateAccessed,
    Hidden
}

internal enum QueryFilterApplyMode
{
    Set,
    Toggle,
    Clear
}

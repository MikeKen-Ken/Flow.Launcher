namespace Flow.Launcher.SearchFilters;

internal enum QueryFilterId
{
    File,
    Folder,
    Path,
    Image,
    Video,
    Audio,
    Document,
    Size,
    DateModified,
    DateCreated,
    Archive,
    Executable,
    Extension,
    DateAccessed,
    Hidden
}

internal enum QueryFilterGroup
{
    Kind,
    Path,
    Type,
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

namespace Flow.Launcher.SearchFilters;

internal enum QueryFilterId
{
    File,
    Folder,
    Image,
    Video,
    Audio,
    Document,
    Size,
    DateModified,
    DateCreated
}

internal enum QueryFilterGroup
{
    Kind,
    Type,
    Size,
    DateModified,
    DateCreated
}

internal enum QueryFilterApplyMode
{
    Set,
    Toggle,
    Clear
}

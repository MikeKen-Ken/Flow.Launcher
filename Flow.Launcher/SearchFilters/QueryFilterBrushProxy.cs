using System.Windows;

namespace Flow.Launcher.SearchFilters;

public class QueryFilterBrushProxy : Freezable
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data),
        typeof(QueryFilterBar),
        typeof(QueryFilterBrushProxy));

    public QueryFilterBar Data
    {
        get => (QueryFilterBar)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new QueryFilterBrushProxy();
}

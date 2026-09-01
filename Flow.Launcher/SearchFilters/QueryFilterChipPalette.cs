using System.Windows.Media;

namespace Flow.Launcher.SearchFilters;

internal readonly record struct QueryFilterChipBrushes(
    SolidColorBrush Fill,
    SolidColorBrush FillHover,
    SolidColorBrush Text,
    SolidColorBrush Stroke,
    SolidColorBrush SelectedFill,
    SolidColorBrush SelectedText,
    SolidColorBrush PanelFill,
    SolidColorBrush PanelStroke,
    SolidColorBrush FieldFill);

internal static class QueryFilterChipPalette
{
    private static readonly Color FallbackDarkSurface = Color.FromRgb(0x2F, 0x2F, 0x2F);
    private static readonly Color FallbackAccent = Color.FromRgb(0x00, 0x78, 0xD4);

    internal static QueryFilterChipBrushes Create(Color text, Color surface, Color accent)
    {
        surface = Opaque(surface);
        text = Opaque(text);
        accent = Opaque(accent.A == 0 ? FallbackAccent : accent);

        var darkTheme = Luminance(text) >= 0.45;
        var toward = darkTheme ? Colors.White : Colors.Black;
        var fill = Mix(surface, toward, darkTheme ? 0.26 : 0.10);
        var fillHover = Mix(surface, toward, darkTheme ? 0.36 : 0.16);
        var stroke = Mix(fill, toward, darkTheme ? 0.22 : 0.18);
        var chipText = darkTheme ? Mix(text, Colors.White, 0.45) : Mix(text, Colors.Black, 0.12);
        var panel = Mix(surface, toward, darkTheme ? 0.10 : 0.04);
        var panelStroke = Mix(panel, toward, darkTheme ? 0.28 : 0.16);
        var field = Mix(panel, Colors.Black, darkTheme ? 0.18 : 0.04);

        return new QueryFilterChipBrushes(
            Brush(fill),
            Brush(fillHover),
            Brush(chipText),
            Brush(stroke),
            Brush(accent),
            Brush(ContrastingText(accent)),
            Brush(panel),
            Brush(panelStroke),
            Brush(field));
    }

    internal static Color FallbackSurface => FallbackDarkSurface;

    internal static Color FallbackAccentColor => FallbackAccent;

    internal static double ContrastRatio(Color a, Color b)
    {
        var l1 = Luminance(a);
        var l2 = Luminance(b);
        var light = l1 > l2 ? l1 : l2;
        var dark = l1 > l2 ? l2 : l1;
        return (light + 0.05) / (dark + 0.05);
    }

    private static Color ContrastingText(Color background) =>
        Luminance(background) > 0.55 ? Color.FromRgb(0x1A, 0x1A, 0x1A) : Colors.White;

    private static Color Opaque(Color color) => Color.FromRgb(color.R, color.G, color.B);

    private static Color Mix(Color from, Color to, double amount)
    {
        amount = amount < 0 ? 0 : amount > 1 ? 1 : amount;
        return Color.FromRgb(
            MixChannel(from.R, to.R, amount),
            MixChannel(from.G, to.G, amount),
            MixChannel(from.B, to.B, amount));
    }

    private static byte MixChannel(byte from, byte to, double amount) =>
        (byte)(from + (((int)to - from) * amount));

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var srgb = value / 255d;
            return srgb <= 0.03928 ? srgb / 12.92 : System.Math.Pow((srgb + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }

    private static SolidColorBrush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }
}

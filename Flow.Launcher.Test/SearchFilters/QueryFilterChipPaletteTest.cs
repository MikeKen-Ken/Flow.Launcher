using System.Windows.Media;
using Flow.Launcher.SearchFilters;
using NUnit.Framework;

namespace Flow.Launcher.Test.SearchFilters;

public class QueryFilterChipPaletteTest
{
    [Test]
    public void DarkThemePalette_KeepsLightTextReadableOnElevatedFill()
    {
        var palette = QueryFilterChipPalette.Create(
            Color.FromRgb(0xE3, 0xE0, 0xE3),
            Color.FromRgb(0x2F, 0x2F, 0x2F),
            Color.FromRgb(0x00, 0x78, 0xD4));

        Assert.That(palette.Fill.Color.A, Is.EqualTo((byte)255));
        Assert.That(
            QueryFilterChipPalette.ContrastRatio(palette.Text.Color, palette.Fill.Color),
            Is.GreaterThanOrEqualTo(4.5));
        Assert.That(palette.Fill.Color, Is.Not.EqualTo(Color.FromRgb(0x2F, 0x2F, 0x2F)));
    }

    [Test]
    public void LightThemePalette_KeepsDarkTextReadableOnElevatedFill()
    {
        var palette = QueryFilterChipPalette.Create(
            Color.FromRgb(0x1A, 0x1A, 0x1A),
            Color.FromRgb(0xF3, 0xF3, 0xF3),
            Color.FromRgb(0x00, 0x78, 0xD4));

        Assert.That(
            QueryFilterChipPalette.ContrastRatio(palette.Text.Color, palette.Fill.Color),
            Is.GreaterThanOrEqualTo(4.5));
        Assert.That(QueryFilterChipPalette.ContrastRatio(palette.SelectedText.Color, palette.SelectedFill.Color),
            Is.GreaterThanOrEqualTo(4.5));
    }
}

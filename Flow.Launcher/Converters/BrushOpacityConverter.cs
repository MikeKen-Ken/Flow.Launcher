using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Flow.Launcher.Converters;

public class BrushOpacityConverter : IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
    {
        var opacity = 0.32;
        if (parameter is string text
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            opacity = parsed;
        }

        if (value is not SolidColorBrush source)
        {
            var alpha = (byte)(opacity * 255);
            return new SolidColorBrush(Color.FromArgb(alpha, 245, 245, 245));
        }

        var color = source.Color;
        return new SolidColorBrush(Color.FromArgb(
            (byte)(opacity * 255),
            color.R,
            color.G,
            color.B));
    }

    public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture) =>
        throw new System.InvalidOperationException();
}

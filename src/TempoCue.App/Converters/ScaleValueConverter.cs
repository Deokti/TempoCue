using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TempoCue.App.Converters;

/// <summary>
/// Масштабирует числовой размер относительно родительского элемента.
/// Используется для адаптивных областей, которым WPF не позволяет задать размер в процентах напрямую.
/// </summary>
public sealed class ScaleValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double source
            || !TryGetScale(parameter, out var scale))
        {
            return DependencyProperty.UnsetValue;
        }

        return source * scale;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException("Обратное преобразование размера не поддерживается.");
    }

    private static bool TryGetScale(object parameter, out double scale)
    {
        scale = 0;

        return parameter is not null
            && double.TryParse(
                parameter.ToString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out scale)
            && scale > 0;
    }
}

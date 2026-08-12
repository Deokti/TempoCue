using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TempoCue.App.Converters;

/// <summary>Размещает маркер временной шкалы внутри доступной ширины Canvas.</summary>
public sealed class TimelinePositionConverter : IMultiValueConverter
{
    private const double MarkerWidth = 150;

    public object Convert(
        object[] values,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (values.Length < 2
            || values[0] is not double ratio
            || values[1] is not double width
            || double.IsNaN(width))
        {
            return DependencyProperty.UnsetValue;
        }

        return Math.Clamp(ratio, 0, 1) * Math.Max(0, width - MarkerWidth);
    }

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture) =>
        throw new NotSupportedException("Положение маркера временной шкалы вычисляется только из времени.");
}

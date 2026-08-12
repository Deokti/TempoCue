using System.Globalization;
using System.Windows;
using TempoCue.App.Converters;

namespace TempoCue.App.Tests;

/// <summary>Проверяет общий расчёт адаптивных размеров элементов интерфейса.</summary>
public sealed class ScaleValueConverterTests
{
    [Theory]
    [InlineData(1280, "0.70", 896)]
    [InlineData(1280, "0.78", 998.4)]
    [InlineData(720, "0.80", 576)]
    public void Convert_MultipliesSizeUsingInvariantScale(
        double source,
        string scale,
        double expected)
    {
        var converter = new ScaleValueConverter();

        var result = converter.Convert(
            source,
            typeof(double),
            scale,
            CultureInfo.GetCultureInfo("ru-RU"));

        Assert.Equal(expected, Assert.IsType<double>(result), 2);
    }

    [Fact]
    public void Convert_ReturnsUnsetValueForInvalidScale()
    {
        var converter = new ScaleValueConverter();

        var result = converter.Convert(
            1280d,
            typeof(double),
            "не число",
            CultureInfo.InvariantCulture);

        Assert.Same(DependencyProperty.UnsetValue, result);
    }
}

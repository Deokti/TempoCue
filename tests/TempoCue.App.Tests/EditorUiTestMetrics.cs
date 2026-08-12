using System.Windows;

namespace TempoCue.App.Tests;

/// <summary>Общие размеры полей редактора, проверяемые несколькими UI-сценариями.</summary>
internal static class EditorUiTestMetrics
{
    internal static Thickness SharedTextBoxPadding { get; } = new(12, 0, 12, 0);

    internal static double CalculateResponsiveSize(
        double actualOwnerSize,
        double scale,
        double minimum,
        double maximum) =>
        Math.Clamp(actualOwnerSize * scale, minimum, maximum);

    internal static double CalculateAvailableWindowSize(
        double workAreaSize,
        double minimumWindowSize) =>
        Math.Max(workAreaSize, minimumWindowSize);
}

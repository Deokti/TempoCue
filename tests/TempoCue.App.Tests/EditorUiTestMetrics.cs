using System.Windows;

namespace TempoCue.App.Tests;

/// <summary>Общие размеры полей редактора, проверяемые несколькими UI-сценариями.</summary>
internal static class EditorUiTestMetrics
{
    internal static Thickness SharedTextBoxPadding { get; } = new(12, 0, 12, 0);
}

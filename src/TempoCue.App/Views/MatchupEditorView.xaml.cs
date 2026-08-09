using System.Windows;
using System.Windows.Controls;

namespace TempoCue.App.Views;

/// <summary>Каркас последовательного редактора матчапа.</summary>
public partial class MatchupEditorView : UserControl
{
    public MatchupEditorView()
    {
        InitializeComponent();
    }

    public event EventHandler? BackRequested;

    private void BackToPackTypeButton_Click(object sender, RoutedEventArgs e) =>
        BackRequested?.Invoke(this, EventArgs.Empty);
}

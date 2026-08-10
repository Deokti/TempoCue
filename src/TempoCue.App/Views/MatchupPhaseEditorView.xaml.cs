using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Форма рабочей копии новой или выбранной фазы матчапа.</summary>
public partial class MatchupPhaseEditorView : UserControl
{
    public MatchupPhaseEditorView()
    {
        InitializeComponent();
    }

    private MatchupContentViewModel ViewModel => (MatchupContentViewModel)DataContext;

    private MatchupPhaseEditorViewModel Editor => ViewModel.Editor;

    private void SavePhaseButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.SaveEditor();

    private void DuplicateEditedPhaseButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DuplicateEditor();

    private void DeleteEditedPhaseButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DeleteEditor();

    private void DecreaseMinimumLevelButton_Click(object sender, RoutedEventArgs e) =>
        Editor.DecreaseMinimumLevel();

    private void IncreaseMinimumLevelButton_Click(object sender, RoutedEventArgs e) =>
        Editor.IncreaseMinimumLevel();

    private void DecreaseMaximumLevelButton_Click(object sender, RoutedEventArgs e) =>
        Editor.DecreaseMaximumLevel();

    private void IncreaseMaximumLevelButton_Click(object sender, RoutedEventArgs e) =>
        Editor.IncreaseMaximumLevel();
}

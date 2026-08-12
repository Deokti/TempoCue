using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TempoCue.App.ViewModels;
using TempoCue.Application;

namespace TempoCue.App.Views;

/// <summary>Каркас последовательного редактора матчапа.</summary>
public partial class MatchupEditorView : UserControl
{
    public MatchupEditorView()
    {
        InitializeComponent();
    }

    public event EventHandler? BackRequested;

    public event EventHandler? SaveCompleted;

    private MatchupEditorViewModel ViewModel =>
        (MatchupEditorViewModel)DataContext;

    // Навигация меняет активный шаг, а на первом шаге возвращает к выбору типа набора.
    private void BackToPackTypeButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.MovePrevious())
        {
            EditorWizardNavigation.ResetScroll(MatchupEditorContentScrollViewer);
        }
        else
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void MatchupEditorNextButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.MoveNext())
        {
            EditorWizardNavigation.ResetScroll(MatchupEditorContentScrollViewer);
        }
    }

    private void CloseMatchupEditorButton_Click(object sender, RoutedEventArgs e) =>
        BackRequested?.Invoke(this, EventArgs.Empty);

    private void MatchupEditorStepsListBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        NavigateToStep(MatchupEditorStepsListBox.SelectedIndex + 1);

    private void MatchupEditorStepsListBox_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!EditorWizardNavigation.TryGetStep(e, out var selectedStep))
        {
            return;
        }

        NavigateToStep(selectedStep);
        e.Handled = true;
    }

    private void NavigateToStep(int selectedStep)
    {
        EditorWizardNavigation.Navigate(
            ViewModel,
            MatchupEditorStepsListBox,
            MatchupEditorContentScrollViewer,
            selectedStep);
    }

    private async void SaveDraftButton_Click(object sender, RoutedEventArgs e) =>
        await SaveAndCloseAsync(LocalCoachPackState.Draft);

    private async void SaveAndEnableButton_Click(object sender, RoutedEventArgs e) =>
        await SaveAndCloseAsync(LocalCoachPackState.Enabled);

    private async void SaveChangesButton_Click(object sender, RoutedEventArgs e) =>
        await SaveAndCloseAsync(ViewModel.Save.OriginalState);

    private async Task SaveAndCloseAsync(LocalCoachPackState state)
    {
        if (await ViewModel.Save.SaveAsync(state))
        {
            SaveCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

}

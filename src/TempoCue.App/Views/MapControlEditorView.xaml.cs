using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TempoCue.App.ViewModels;
using TempoCue.Application;

namespace TempoCue.App.Views;

/// <summary>Каркас последовательного редактора контроля карты.</summary>
public partial class MapControlEditorView : UserControl
{
    public MapControlEditorView()
    {
        InitializeComponent();
    }

    public event EventHandler? BackRequested;

    public event EventHandler? SaveCompleted;

    private MapControlEditorViewModel ViewModel =>
        (MapControlEditorViewModel)DataContext;

    private void MapControlEditorBackButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.MovePrevious())
        {
            EditorWizardNavigation.ResetScroll(MapControlEditorContentScrollViewer);
        }
        else
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void MapControlEditorNextButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.MoveNext())
        {
            EditorWizardNavigation.ResetScroll(MapControlEditorContentScrollViewer);
        }
    }

    private void CloseMapControlEditorButton_Click(object sender, RoutedEventArgs e) =>
        BackRequested?.Invoke(this, EventArgs.Empty);

    private async void MapControlSaveDraftButton_Click(object sender, RoutedEventArgs e) =>
        await SaveAndCloseAsync(LocalCoachPackState.Draft);

    private async void MapControlSaveAndEnableButton_Click(object sender, RoutedEventArgs e) =>
        await SaveAndCloseAsync(LocalCoachPackState.Enabled);

    private async Task SaveAndCloseAsync(LocalCoachPackState state)
    {
        if (await ViewModel.Save.SaveAsync(state))
        {
            SaveCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    private void MapControlEditorStepsListBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        NavigateToStep(MapControlEditorStepsListBox.SelectedIndex + 1);

    private void MapControlEditorStepsListBox_PreviewMouseLeftButtonDown(
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
            MapControlEditorStepsListBox,
            MapControlEditorContentScrollViewer,
            selectedStep);
    }
}

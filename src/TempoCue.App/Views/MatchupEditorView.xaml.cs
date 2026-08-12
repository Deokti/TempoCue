using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
            ResetContentScroll();
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
            ResetContentScroll();
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
        if (sender is not ListBox
            || FindStepItem(e.OriginalSource as DependencyObject) is not { IsEnabled: true } stepItem
            || !int.TryParse(stepItem.Tag?.ToString(), out var selectedStep))
        {
            return;
        }

        NavigateToStep(selectedStep);
        e.Handled = true;
    }

    private void NavigateToStep(int selectedStep)
    {
        if (selectedStep < 1
            || selectedStep > ViewModel.TotalSteps
            || selectedStep == ViewModel.CurrentStep)
        {
            return;
        }

        if (ViewModel.NavigateToStep(selectedStep))
        {
            ResetContentScroll();
        }

        MatchupEditorStepsListBox.SetCurrentValue(
            Selector.SelectedIndexProperty,
            ViewModel.CurrentStepIndex);
    }

    private static ListBoxItem? FindStepItem(DependencyObject? element)
    {
        while (element is not null and not ListBoxItem)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        return element as ListBoxItem;
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

    // Каждый новый шаг начинается с верхней границы содержимого независимо от позиции предыдущего.
    private void ResetContentScroll() =>
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(MatchupEditorContentScrollViewer.ScrollToTop));
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Каркас последовательного редактора матчапа.</summary>
public partial class MatchupEditorView : UserControl
{
    public MatchupEditorView()
    {
        InitializeComponent();
    }

    public event EventHandler? BackRequested;

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

    // Каждый новый шаг начинается с верхней границы содержимого независимо от позиции предыдущего.
    private void ResetContentScroll() =>
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(MatchupEditorContentScrollViewer.ScrollToTop));
}

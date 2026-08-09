using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Библиотека матчапов, правил карты и пользовательских Coach Packs.</summary>
public partial class CoachingView : UserControl
{
    public CoachingView()
    {
        InitializeComponent();
    }

    private CoachingViewModel ViewModel =>
        (CoachingViewModel)DataContext;

    private void CreatePackButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenPackTypeSelection();

    private void PackTypeSelectionPage_BackRequested(object? sender, EventArgs e) =>
        ViewModel.OpenLibrary();

    private void PackTypeSelectionPage_MatchupSelected(object? sender, EventArgs e) =>
        ViewModel.OpenMatchupEditor();

    private void MatchupEditorPage_BackRequested(object? sender, EventArgs e) =>
        ViewModel.OpenPackTypeSelection();
}

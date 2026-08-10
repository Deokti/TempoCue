using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Read-only просмотр сохранённого Coach Pack и вход в экспорт предметов.</summary>
public partial class CoachingPackDetailsView : UserControl
{
    public CoachingPackDetailsView()
    {
        InitializeComponent();
    }

    public event EventHandler? BackRequested;

    public event EventHandler? StateChangeRequested;

    private CoachingPackDetailsViewModel ViewModel =>
        (CoachingPackDetailsViewModel)DataContext;

    private void BackButton_Click(object sender, RoutedEventArgs e) =>
        BackRequested?.Invoke(this, EventArgs.Empty);

    private void StateActionButton_Click(object sender, RoutedEventArgs e) =>
        StateChangeRequested?.Invoke(this, EventArgs.Empty);

    private void OverviewTabButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ShowOverview();

    private void AdviceTabButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ShowAdvice();

    private void ItemsTabButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ShowItems();

    private async void ExportButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.OpenExportAsync();
}

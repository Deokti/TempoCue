using System.Windows;
using TempoCue.App.Localization;
using TempoCue.App.ViewModels;

namespace TempoCue.App;

/// <summary>
/// Главное окно TempoCue. Поведение разделено по partial-файлам:
/// навигация, подключение к матчу и коучинг развиваются независимо друг от друга.
/// </summary>
public partial class MainWindow : Window
{
    private readonly CurrentMatchViewModel _currentMatchViewModel;
    private readonly LocalizationService _localization = LocalizationService.Instance;

    public MainWindow(
        CurrentMatchViewModel currentMatchViewModel,
        HomeViewModel homeViewModel,
        SettingsViewModel settingsViewModel)
    {
        ArgumentNullException.ThrowIfNull(currentMatchViewModel);
        ArgumentNullException.ThrowIfNull(homeViewModel);
        ArgumentNullException.ThrowIfNull(settingsViewModel);
        _currentMatchViewModel = currentMatchViewModel;

        InitializeComponent();
        HomeContentPage.DataContext = homeViewModel;
        SettingsContentPage.DataContext = settingsViewModel;
        CurrentMatchContentPage.DataContext = _currentMatchViewModel;
        HomeContentPage.OpenCurrentMatchRequested += HomeContentPage_OpenCurrentMatchRequested;

        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _currentMatchViewModel.StateUpdated += CurrentMatchViewModel_StateUpdated;
        _currentMatchViewModel.Start();
        RenderShellFromCurrentMatch();
    }

    private void HomeContentPage_OpenCurrentMatchRequested(object? sender, EventArgs e) =>
        NavigateTo(AppPage.CurrentMatch);
}

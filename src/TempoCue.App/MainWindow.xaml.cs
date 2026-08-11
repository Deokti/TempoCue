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
        SettingsViewModel settingsViewModel,
        CoachingViewModel coachingViewModel)
    {
        ArgumentNullException.ThrowIfNull(currentMatchViewModel);
        ArgumentNullException.ThrowIfNull(homeViewModel);
        ArgumentNullException.ThrowIfNull(settingsViewModel);
        ArgumentNullException.ThrowIfNull(coachingViewModel);
        _currentMatchViewModel = currentMatchViewModel;

        // Главное окно является composition root для экранов: здесь им назначаются готовые ViewModel.
        InitializeComponent();
        WindowWorkAreaBehavior.Attach(this);
        HomeContentPage.DataContext = homeViewModel;
        SettingsContentPage.DataContext = settingsViewModel;
        CoachingContentPage.DataContext = coachingViewModel;
        CurrentMatchContentPage.DataContext = _currentMatchViewModel;
        HomeContentPage.OpenCurrentMatchRequested += HomeContentPage_OpenCurrentMatchRequested;

        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;
        Closed += MainWindow_Closed;
    }

    // После загрузки окно подписывается только на представление состояния;
    // единый цикл polling принадлежит ActiveMatchMonitor в composition root.
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _currentMatchViewModel.StateUpdated += CurrentMatchViewModel_StateUpdated;
        RenderShellFromCurrentMatch();
    }

    private void HomeContentPage_OpenCurrentMatchRequested(object? sender, EventArgs e) =>
        NavigateTo(AppPage.CurrentMatch);
}

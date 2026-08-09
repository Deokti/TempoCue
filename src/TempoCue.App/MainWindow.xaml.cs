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

    public MainWindow(CurrentMatchViewModel currentMatchViewModel)
    {
        ArgumentNullException.ThrowIfNull(currentMatchViewModel);
        _currentMatchViewModel = currentMatchViewModel;

        InitializeComponent();
        DataContext = _currentMatchViewModel;

        // Названия языков показаны на самих языках: «Русский» и «English».
        LanguageComboBox.SelectedValue = _localization.CurrentLanguageCode;
        _isWindowInitialized = true;

        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _currentMatchViewModel.StateUpdated += CurrentMatchViewModel_StateUpdated;
        _currentMatchViewModel.Start();
        RenderHomeFromCurrentMatch();
    }
}

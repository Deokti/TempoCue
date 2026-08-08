using System.IO;
using System.Windows;
using TempoCue.App.Localization;
using TempoCue.Core;

namespace TempoCue.App;

/// <summary>
/// Главное окно TempoCue. Поведение разделено по partial-файлам:
/// навигация, подключение к матчу и коучинг развиваются независимо друг от друга.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MatchupCoach _coach;
    private readonly LocalizationService _localization = LocalizationService.Instance;

    public MainWindow()
    {
        InitializeComponent();

        var packPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "CoachPacks",
            "urgot-vs-sett.json");

        var pack = CoachPackLoader.LoadFromFile(packPath);
        _coach = new MatchupCoach(pack.Rules);

        // Названия языков показаны на самих языках: «Русский» и «English».
        LanguageComboBox.SelectedValue = _localization.CurrentLanguageCode;
        _isWindowInitialized = true;

        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ShowInitialWaitingAdvice();

        // Опрос живёт столько же, сколько окно, и штатно отменяется при закрытии.
        _livePollingTask ??= PollLiveMatchAsync(_livePollingCancellation.Token);
    }
}

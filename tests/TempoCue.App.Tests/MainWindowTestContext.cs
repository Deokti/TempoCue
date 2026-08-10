using System.Windows.Controls;
using System.Windows.Threading;
using TempoCue.App.Localization;
using TempoCue.App.Settings;
using TempoCue.App.ViewModels;
using TempoCue.App.Views;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

/// <summary>Собирает главное окно с детерминированными локальными зависимостями для UI-тестов.</summary>
internal sealed class MainWindowTestContext : IDisposable
{
    private readonly string _initialLanguage;
    private bool _isDisposed;

    public MainWindowTestContext()
    {
        Localization = LocalizationService.Instance;
        _initialLanguage = Localization.CurrentLanguageCode;
        Localization.SetLanguage("ru-RU");

        var session = new ActiveMatchSession(
            new WaitingSnapshotSource(),
            new MatchupCoach([]),
            TimeSpan.FromSeconds(1));
        CurrentMatch = new CurrentMatchViewModel(session, Localization);
        Home = new HomeViewModel(CurrentMatch, Localization);
        Settings = new SettingsViewModel(Localization, new AppSettingsService());
        CoachPackStore = new TestCoachPackStore();
        Coaching = new CoachingViewModel(
            CreateCoachPack(),
            Localization,
            TestGamePatchCatalog.Ready(),
            TestChampionCatalog.Ready(),
            TestItemCatalog.Ready(),
            new SaveCoachPackUseCase(CoachPackStore),
            new LoadCoachPackLibraryUseCase(CoachPackStore),
            new SetCoachPackStateUseCase(CoachPackStore));
        Window = new MainWindow(CurrentMatch, Home, Settings, Coaching);
    }

    public LocalizationService Localization { get; }

    public CurrentMatchViewModel CurrentMatch { get; }

    public HomeViewModel Home { get; }

    public SettingsViewModel Settings { get; }

    public TestCoachPackStore CoachPackStore { get; }

    public CoachingViewModel Coaching { get; }

    public MainWindow Window { get; }

    public CoachingView CoachingPage =>
        WpfTestHost.Find<CoachingView>(Window, "CoachingContentPage");

    public CoachingLibraryView CoachingLibraryPage =>
        WpfTestHost.Find<CoachingLibraryView>(CoachingPage, "CoachingLibraryPage");

    public MatchupEditorView MatchupEditorPage =>
        WpfTestHost.Find<MatchupEditorView>(CoachingPage, "MatchupEditorPage");

    public MatchupMainInformationView MainInformationPage =>
        WpfTestHost.Find<MatchupMainInformationView>(
            MatchupEditorPage,
            "MatchupMainInformationPage");

    public MatchupChampionsAndRoleView ChampionsAndRolePage =>
        WpfTestHost.Find<MatchupChampionsAndRoleView>(
            MatchupEditorPage,
            "MatchupChampionsAndRolePage");

    public MatchupContentView ContentPage =>
        WpfTestHost.Find<MatchupContentView>(MatchupEditorPage, "MatchupContentPage");

    public MatchupVerificationView VerificationPage =>
        WpfTestHost.Find<MatchupVerificationView>(MatchupEditorPage, "MatchupVerificationPage");

    public MatchupSaveView SavePage =>
        WpfTestHost.Find<MatchupSaveView>(MatchupEditorPage, "MatchupSavePage");

    public MatchupPhaseEditorView PhaseEditorView =>
        WpfTestHost.Find<MatchupPhaseEditorView>(ContentPage, "PhaseEditorView");

    public MatchupPhaseItemRecommendationsView PhaseItemRecommendationsView =>
        WpfTestHost.Find<MatchupPhaseItemRecommendationsView>(
            PhaseEditorView,
            "ItemRecommendationsView");

    public MatchupPhaseListView PhaseListView =>
        WpfTestHost.Find<MatchupPhaseListView>(ContentPage, "PhaseListView");

    public MatchupPersistentTipsView PersistentTipsView =>
        WpfTestHost.Find<MatchupPersistentTipsView>(ContentPage, "PersistentTipsView");

    public MatchupItemBlocksView ItemBlocksView =>
        WpfTestHost.Find<MatchupItemBlocksView>(ContentPage, "ItemBlocksView");

    public ChampionPickerDialogView ChampionPickerDialog =>
        WpfTestHost.Find<ChampionPickerDialogView>(MatchupEditorPage, "ChampionPickerDialog");

    public ItemPickerDialogView ItemPickerDialog =>
        WpfTestHost.Find<ItemPickerDialogView>(MatchupEditorPage, "ItemPickerDialog");

    public void Show()
    {
        Window.Show();
        WpfTestHost.Pump(DispatcherPriority.Loaded);
    }

    public void OpenMatchupEditor()
    {
        WpfTestHost.Click(WpfTestHost.Find<Button>(Window, "CoachingNavigationButton"));
        WpfTestHost.Click(WpfTestHost.Find<Button>(CoachingLibraryPage, "CreatePackButton"));
        var packTypePage = WpfTestHost.Find<PackTypeSelectionView>(
            CoachingPage,
            "PackTypeSelectionPage");
        WpfTestHost.Click(WpfTestHost.Find<Button>(packTypePage, "MatchupPackTypeButton"));
    }

    public void CompleteMainInformationAndOpenChampions()
    {
        var mainInformation = Coaching.MatchupEditor.MainInformation;
        mainInformation.PackName = "Urgot против Sett";
        mainInformation.ShortDescription = "Как пережить раннюю линию и найти момент для боя.";
        mainInformation.Author = "TopLaneAuthor";
        mainInformation.EnsureGamePatchCatalogLoadedAsync().GetAwaiter().GetResult();
        WpfTestHost.Pump();
        WpfTestHost.Click(WpfTestHost.Find<Button>(MatchupEditorPage, "MatchupEditorNextButton"));
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
    }

    public void SelectChampionsAndOpenContent()
    {
        var champions = Coaching.MatchupEditor.ChampionsAndRole;
        champions.EnsureCatalogLoadedAsync().GetAwaiter().GetResult();
        champions.SelectedPlayerChampion = champions.Champions.Single(
            static champion => champion.Id == "Urgot");
        champions.SelectedOpponentChampion = champions.Champions.Single(
            static champion => champion.Id == "Sett");
        WpfTestHost.Pump();
        WpfTestHost.Click(WpfTestHost.Find<Button>(MatchupEditorPage, "MatchupEditorNextButton"));
    }

    public void OpenMatchupContent()
    {
        OpenMatchupEditor();
        CompleteMainInformationAndOpenChampions();
        SelectChampionsAndOpenContent();
    }

    public MatchupPhaseViewModel AddPhase(
        string name,
        int minimumLevel,
        int? maximumLevel,
        string primaryAdvice,
        PowerState powerState = PowerState.Dangerous,
        string whyNow = "",
        string nextGoal = "")
    {
        var content = Coaching.MatchupEditor.Content;
        content.BeginAddPhase();
        content.Editor.Name = name;
        content.Editor.SelectedPowerState = powerState;
        while (content.Editor.MinimumLevel < minimumLevel)
        {
            content.Editor.IncreaseMinimumLevel();
        }

        content.Editor.ExtendsToEnd = maximumLevel is null;
        if (maximumLevel is int lastLevel)
        {
            while (content.Editor.MaximumLevel > lastLevel)
            {
                content.Editor.DecreaseMaximumLevel();
            }
        }

        content.Editor.PrimaryAdvice = primaryAdvice;
        content.Editor.WhyNow = whyNow;
        content.Editor.NextGoal = nextGoal;
        if (!content.SaveEditor())
        {
            throw new InvalidOperationException("Не удалось сохранить тестовую фазу матчапа.");
        }

        return content.Phases[^1];
    }

    public void OpenSaveStep()
    {
        OpenMatchupContent();
        AddPhase(
            "Осторожное начало",
            1,
            8,
            "Не ищи размен первым",
            PowerState.Dangerous,
            "Sett сильнее в начале",
            "Дождаться девятого уровня");
        AddPhase(
            "Можно играть активнее",
            9,
            null,
            "Используй усиленную W",
            PowerState.Playable);
        WpfTestHost.Click(WpfTestHost.Find<Button>(MatchupEditorPage, "MatchupEditorNextButton"));
        WpfTestHost.Click(WpfTestHost.Find<Button>(MatchupEditorPage, "MatchupEditorNextButton"));
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Window.Close();
        Coaching.Dispose();
        Settings.Dispose();
        Home.Dispose();
        CurrentMatch.Dispose();
        Localization.SetLanguage(_initialLanguage);
    }

    private static CoachPack CreateCoachPack() => new()
    {
        SchemaVersion = 1,
        Id = "test.urgot-vs-sett",
        Name = "Urgot vs Sett",
        Patch = "test",
        Rules =
        [
            new CoachRuleDefinition
            {
                Id = "early",
                PlayerChampion = "Urgot",
                OpponentChampion = "Sett",
                MinimumLevel = 1,
                MaximumLevel = 8,
                PowerState = PowerState.Dangerous,
                Priority = 1,
                Title = "Test title",
                Message = "Test message",
                Source = "Test source"
            }
        ]
    };

    private sealed class WaitingSnapshotSource : IMatchSnapshotSource
    {
        public Task<MatchSnapshotReadResult> ReadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(MatchSnapshotReadResult.NoActiveMatch());
    }
}

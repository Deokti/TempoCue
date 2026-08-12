using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.App.Views;
using TempoCue.Application;

namespace TempoCue.App.Tests;

/// <summary>Проверяет переводы интерфейса и формы слов для обеих локалей.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class LocalizationServiceTests
{
    [Fact]
    public void ChampionsAndRoleView_ShowsLoadingLayoutFromDesign() => WpfTestHost.Run(() =>
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("ru-RU");
        try
        {
            var catalog = new DeferredChampionCatalog();
            var viewModel = new MatchupChampionsAndRoleViewModel(catalog, localization);
            var loadTask = viewModel.EnsureCatalogLoadedAsync();
            var view = new MatchupChampionsAndRoleView
            {
                DataContext = viewModel
            };
            var window = new Window
            {
                Width = 760,
                Height = 560,
                Content = view,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };

            window.Show();
            WpfTestHost.Pump(DispatcherPriority.Render);

            Assert.Equal(
                Visibility.Visible,
                WpfTestHost.Find<StackPanel>(view, "ChampionCatalogLoadingPanel").Visibility);
            Assert.Equal(
                Visibility.Collapsed,
                WpfTestHost.Find<StackPanel>(view, "ChampionSelectionContent").Visibility);
            Assert.Equal(
                Visibility.Visible,
                WpfTestHost.Find<StackPanel>(view, "MatchupRoleSelectionPanel").Visibility);
            Assert.Equal(
                86d,
                WpfTestHost.Find<Border>(view, "PlayerChampionLoadingCard").ActualHeight);
            Assert.Equal(
                new CornerRadius(12),
                WpfTestHost.Find<Border>(view, "OpponentChampionLoadingCard").CornerRadius);
            Assert.Equal(5, WpfTestHost.Find<ListBox>(view, "MatchupRoleListBox").Items.Count);

            catalog.Complete(ChampionCatalogLoadResult.Unavailable());
            WpfTestHost.Pump(DispatcherPriority.Background);
            loadTask.GetAwaiter().GetResult();
            window.Close();
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    });

    [Fact]
    public void MainWindow_UpdatesVisibleTranslationsWithoutRestart() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        context.Show();
        var settingsPage = WpfTestHost.Find<SettingsView>(context.Window, "SettingsContentPage");
        var settingsTitle = WpfTestHost.Find<TextBlock>(settingsPage, "SettingsTitleTextBlock");
        var languageComboBox = WpfTestHost.Find<ComboBox>(settingsPage, "LanguageComboBox");
        var packTypePage = WpfTestHost.Find<PackTypeSelectionView>(
            context.CoachingPage,
            "PackTypeSelectionPage");
        WpfTestHost.Click(WpfTestHost.Find<Button>(context.Window, "SettingsNavigationButton"));
        context.Window.UpdateLayout();
        WpfTestHost.Pump(DispatcherPriority.Render);

        context.Coaching.MatchupEditor.MainInformation
            .EnsureGamePatchCatalogLoadedAsync()
            .GetAwaiter()
            .GetResult();
        WpfTestHost.Pump();
        var patchComboBox = WpfTestHost.Find<ComboBox>(
            context.MainInformationPage,
            "MatchupPatchComboBox");
        Assert.Equal("26.15 — текущий", patchComboBox.Text);
        Assert.Contains(
            WpfTestHost.FindVisualDescendants<TextBlock>(languageComboBox),
            textBlock => textBlock.Text == context.Settings.Languages[0].DisplayName);

        context.Localization.SetLanguage("en-US");
        WpfTestHost.Pump();

        Assert.Equal("TempoCue — League of Legends assistant", context.Window.Title);
        Assert.Equal("Settings", settingsTitle.Text);
        Assert.Equal("26.15 — current", patchComboBox.Text);
        Assert.Equal(
            "What kind of advice pack are we creating?",
            WpfTestHost.Find<TextBlock>(packTypePage, "PackTypeTitleTextBlock").Text);
        Assert.Equal(
            "Champions and role",
            WpfTestHost.Find<TextBlock>(
                context.ChampionsAndRolePage,
                "MatchupChampionsStepTitleTextBlock").Text);

        context.Coaching.MatchupEditor.ChampionsAndRole
            .EnsureCatalogLoadedAsync()
            .GetAwaiter()
            .GetResult();
        WpfTestHost.Pump(DispatcherPriority.Render);
        Assert.Contains(
            WpfTestHost.FindVisualDescendants<TextBlock>(languageComboBox),
            textBlock => textBlock.Text == context.Settings.Languages[1].DisplayName);
    });

    [Fact]
    public void SetLanguage_SwitchesTranslationsAndPluralForms()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;

        try
        {
            localization.SetLanguage("en-US");

            Assert.Equal("Game state simulator", localization.Get("Header.Title"));
            Assert.Equal("Home", localization.Get("Nav.Home"));
            Assert.Equal("Settings", localization.Get("Settings.Title"));
            Assert.Equal("Coaching", localization.Get("Coaching.Title"));
            Assert.Equal("Game control", localization.Get("Coaching.Tab.MapControl"));
            Assert.Equal("Interface language", localization.Get("Settings.Language.Label"));
            Assert.Equal("Available now", localization.Get("Home.Available.Title"));
            Assert.Equal("v0.3.0", AppVersion.Display);
            Assert.Equal("1 player", localization.Plural("Count.Player", 1));
            Assert.Equal("10 players", localization.Plural("Count.Player", 10));

            localization.SetLanguage("ru-RU");

            Assert.Equal("Симулятор игрового состояния", localization.Get("Header.Title"));
            Assert.Equal("Главная", localization.Get("Nav.Home"));
            Assert.Equal("Настройки", localization.Get("Settings.Title"));
            Assert.Equal("Коучинг", localization.Get("Coaching.Title"));
            Assert.Equal("Контроль игры", localization.Get("Coaching.Tab.MapControl"));
            Assert.Equal("Язык интерфейса", localization.Get("Settings.Language.Label"));
            Assert.Equal("Что работает сейчас", localization.Get("Home.Available.Title"));
            Assert.Equal("1 игрок", localization.Plural("Count.Player", 1));
            Assert.Equal("2 игрока", localization.Plural("Count.Player", 2));
            Assert.Equal("10 игроков", localization.Plural("Count.Player", 10));
            Assert.Equal("21 игрок", localization.Plural("Count.Player", 21));
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }

    [Theory]
    [InlineData("ru", "ru-RU")]
    [InlineData("ru-KZ", "ru-RU")]
    [InlineData("en-GB", "en-US")]
    [InlineData("de-DE", "en-US")]
    [InlineData(null, "en-US")]
    public void NormalizeLanguageCode_SupportsOnlyRussianAndEnglish(
        string? languageCode,
        string expected)
    {
        Assert.Equal(expected, LocalizationService.NormalizeLanguageCode(languageCode));
    }
}

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

/// <summary>Проверяет переводы интерфейса и формы слов для обоих языков.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class LocalizationServiceTests
{
    [Fact]
    public void MainWindow_LoadsLocalizedXamlAndUpdatesTitleWithoutRestart()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var localization = LocalizationService.Instance;
                localization.SetLanguage("ru-RU");

                var session = new ActiveMatchSession(
                    new WaitingSnapshotSource(),
                    new MatchupCoach([]),
                    TimeSpan.FromSeconds(1));
                var window = new MainWindow(
                    new CurrentMatchViewModel(session, localization));
                window.Show();
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.Loaded);
                Assert.Equal("TempoCue — ассистент для League of Legends", window.Title);
                Assert.Equal(1280, window.Width);
                Assert.Equal(720, window.Height);
                Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
                Assert.Equal(WindowStyle.None, window.WindowStyle);
                var currentMatchPage = Assert.IsType<TempoCue.App.Views.CurrentMatchView>(
                    window.FindName("CurrentMatchContentPage"));
                var currentMatchButton = Assert.IsType<Button>(
                    window.FindName("CurrentMatchNavigationButton"));
                var settingsPage = Assert.IsType<Grid>(window.FindName("SettingsContentPage"));
                var settingsButton = Assert.IsType<Button>(window.FindName("SettingsNavigationButton"));
                var settingsTitle = Assert.IsType<TextBlock>(window.FindName("SettingsTitleTextBlock"));
                Assert.NotNull(window.FindName("LanguageComboBox"));

                currentMatchButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(Visibility.Visible, currentMatchPage.Visibility);

                settingsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(Visibility.Visible, settingsPage.Visibility);
                Assert.Equal("Настройки", settingsTitle.Text);

                localization.SetLanguage("en-US");
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);

                Assert.Equal("TempoCue — League of Legends assistant", window.Title);
                Assert.Equal("Settings", settingsTitle.Text);
                window.Close();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

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
            Assert.Equal("Interface language", localization.Get("Settings.Language.Label"));
            Assert.Equal("Available now", localization.Get("Home.Available.Title"));
            Assert.Equal("v0.2.0", AppVersion.Display);
            Assert.Equal("1 player", localization.Plural("Count.Player", 1));
            Assert.Equal("10 players", localization.Plural("Count.Player", 10));

            localization.SetLanguage("ru-RU");

            Assert.Equal("Симулятор игрового состояния", localization.Get("Header.Title"));
            Assert.Equal("Главная", localization.Get("Nav.Home"));
            Assert.Equal("Настройки", localization.Get("Settings.Title"));
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

    private sealed class WaitingSnapshotSource : IMatchSnapshotSource
    {
        public Task<MatchSnapshotReadResult> ReadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(MatchSnapshotReadResult.NoActiveMatch());
    }
}

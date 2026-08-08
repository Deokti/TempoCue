using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using TempoCue.App.Localization;

namespace TempoCue.App.Tests;

/// <summary>Проверяет переводы интерфейса и формы слов для обоих языков.</summary>
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

                var window = new MainWindow();
                Assert.Equal("TempoCue — ассистент для League of Legends", window.Title);

                localization.SetLanguage("en-US");
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);

                Assert.Equal("TempoCue — League of Legends assistant", window.Title);
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
            Assert.Equal("1 player", localization.Plural("Count.Player", 1));
            Assert.Equal("10 players", localization.Plural("Count.Player", 10));

            localization.SetLanguage("ru-RU");

            Assert.Equal("Симулятор игрового состояния", localization.Get("Header.Title"));
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

using System.Windows;
using System.Windows.Controls;
using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.App.Views;

namespace TempoCue.App.Tests;

/// <summary>
/// Проверяет карточку выбора числа потоков на настоящей разметке. Модель
/// представления сама по себе не доказывает, что список привязан и виден:
/// опечатка в имени свойства обнаруживается только при загрузке окна.
/// </summary>
[Collection(LocalizationCollection.Name)]
public sealed class SettingsRecognitionThreadsViewTests
{
    [Fact]
    public void ThreadsSelector_ShowsMachineOptionsAndKeepsChoice()
    {
        WpfTestHost.Run(() =>
        {
            using var settingsContext = new TemporarySettingsContext();
            var localization = LocalizationService.Instance;
            var initialLanguage = localization.CurrentLanguageCode;
            Window? window = null;

            try
            {
                localization.SetLanguage("ru-RU");
                using var viewModel = new SettingsViewModel(
                    localization,
                    settingsContext.Service,
                    static () => { });
                var view = new SettingsView
                {
                    DataContext = viewModel,
                    Width = 820,
                    Height = 700
                };
                window = new Window
                {
                    Content = view,
                    Width = 860,
                    Height = 740,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None
                };
                window.Show();
                WpfTestHost.Pump();

                var selector = WpfTestHost.Find<ComboBox>(view, "RecognitionThreadsSelector");
                Assert.Equal(viewModel.MinimapRecognitionThreadOptions.Count, selector.Items.Count);
                Assert.Equal(
                    MinimapRecognitionThreadOptionViewModel.AutomaticThreads,
                    selector.SelectedValue);

                var manual = viewModel.MinimapRecognitionThreadOptions
                    .First(option =>
                        option.Threads !=
                        MinimapRecognitionThreadOptionViewModel.AutomaticThreads);
                selector.SelectedValue = manual.Threads;
                WpfTestHost.Pump();

                // Выбор в списке обязан дойти до файла настроек, иначе карточка
                // выглядит работающей, но ничего не меняет при следующем запуске.
                Assert.Equal(manual.Threads, viewModel.MinimapRecognitionThreads);
                Assert.Equal(
                    manual.Threads,
                    settingsContext.Service.Load().MinimapRecognitionThreads);
                Assert.True(viewModel.HasRecognitionThreadsRestartNotice);
            }
            finally
            {
                window?.Close();
                localization.SetLanguage(initialLanguage);
            }
        });
    }

    [Fact]
    public void EveryOption_ExplainsWhatToExpectFromIt()
    {
        WpfTestHost.Run(() =>
        {
            using var settingsContext = new TemporarySettingsContext();
            var localization = LocalizationService.Instance;
            var initialLanguage = localization.CurrentLanguageCode;
            Window? window = null;

            try
            {
                localization.SetLanguage("ru-RU");
                using var viewModel = new SettingsViewModel(
                    localization,
                    settingsContext.Service,
                    static () => { });
                var view = new SettingsView { DataContext = viewModel, Width = 820, Height = 700 };
                window = new Window
                {
                    Content = view,
                    Width = 860,
                    Height = 740,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None
                };
                window.Show();
                WpfTestHost.Pump();

                // Каждый вариант обязан объяснять свои последствия: без этого выбор
                // числа потоков превращается в угадывание.
                Assert.All(
                    viewModel.MinimapRecognitionThreadOptions,
                    option => Assert.False(string.IsNullOrWhiteSpace(option.HintText)));
                // Одинаковый текст на всех вариантах не помог бы выбрать: подсказки
                // обязаны различать бережный, сбалансированный и максимальный режим.
                Assert.True(
                    viewModel.MinimapRecognitionThreadOptions
                        .Select(option => option.HintText)
                        .Distinct()
                        .Count() > 1);

                // Подсказка под списком следует за выбором, а не остаётся общей.
                var hint = WpfTestHost.Find<TextBlock>(view, "RecognitionThreadsHintTextBlock");
                var automatic = viewModel.MinimapRecognitionThreadOptions[0];
                Assert.Equal(automatic.HintText, hint.Text);

                var selector = WpfTestHost.Find<ComboBox>(view, "RecognitionThreadsSelector");
                var loneThread = viewModel.MinimapRecognitionThreadOptions
                    .FirstOrDefault(option => option.Threads == 1);
                if (loneThread is null)
                {
                    return;
                }

                selector.SelectedValue = loneThread.Threads;
                WpfTestHost.Pump();

                Assert.Equal(loneThread.HintText, hint.Text);
                // Один поток не просто медленнее: разбор длиннее окна подтверждения,
                // поэтому предупреждение обязано быть видимым.
                var risk = WpfTestHost.Find<TextBlock>(view, "RecognitionThreadsRiskTextBlock");
                Assert.True(loneThread.IsRiskyForNotifications);
                Assert.Equal(Visibility.Visible, risk.Visibility);
            }
            finally
            {
                window?.Close();
                localization.SetLanguage(initialLanguage);
            }
        });
    }

    [Fact]
    public void AutomaticOption_NamesTheNumberItResolvesTo()
    {
        WpfTestHost.Run(() =>
        {
            using var settingsContext = new TemporarySettingsContext();
            var localization = LocalizationService.Instance;
            var initialLanguage = localization.CurrentLanguageCode;

            try
            {
                localization.SetLanguage("ru-RU");
                using var viewModel = new SettingsViewModel(
                    localization,
                    settingsContext.Service,
                    static () => { });

                var automatic = viewModel.MinimapRecognitionThreadOptions[0];

                // Человек выбирает вручную, сравнивая с автоматическим значением,
                // поэтому оно должно быть названо, а не скрыто за словом.
                Assert.Contains(
                    TempoCue.Vision.DefaultMinimapFrameAnalyzer
                        .DefaultDegreeOfParallelism
                        .ToString(),
                    automatic.DisplayName);
            }
            finally
            {
                localization.SetLanguage(initialLanguage);
            }
        });
    }
}

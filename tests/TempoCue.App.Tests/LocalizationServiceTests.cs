using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using TempoCue.App.Localization;
using TempoCue.App.Settings;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

/// <summary>Проверяет переводы интерфейса и формы слов для обоих языков.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class LocalizationServiceTests
{
    [Fact]
    public void ChampionsAndRoleView_ShowsLoadingLayoutFromDesign()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var localization = LocalizationService.Instance;
                localization.SetLanguage("ru-RU");
                var catalog = new DeferredChampionCatalog();
                var viewModel = new MatchupChampionsAndRoleViewModel(catalog, localization);
                var loadTask = viewModel.EnsureCatalogLoadedAsync();
                var view = new TempoCue.App.Views.MatchupChampionsAndRoleView
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
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.Render);

                Assert.Equal(
                    Visibility.Visible,
                    Assert.IsType<StackPanel>(
                        view.FindName("ChampionCatalogLoadingPanel")).Visibility);
                Assert.Equal(
                    Visibility.Collapsed,
                    Assert.IsType<StackPanel>(
                        view.FindName("ChampionSelectionContent")).Visibility);
                Assert.Equal(
                    Visibility.Visible,
                    Assert.IsType<StackPanel>(
                        view.FindName("MatchupRoleSelectionPanel")).Visibility);
                Assert.Equal(
                    86d,
                    Assert.IsType<Border>(
                        view.FindName("PlayerChampionLoadingCard")).ActualHeight);
                Assert.Equal(
                    new CornerRadius(12),
                    Assert.IsType<Border>(
                        view.FindName("OpponentChampionLoadingCard")).CornerRadius);
                Assert.Equal(
                    5,
                    Assert.IsType<ListBox>(
                        view.FindName("MatchupRoleListBox")).Items.Count);

                catalog.Complete(ChampionCatalogLoadResult.Unavailable());
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.Background);
                loadTask.GetAwaiter().GetResult();
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
                using var currentMatchViewModel = new CurrentMatchViewModel(session, localization);
                using var homeViewModel = new HomeViewModel(currentMatchViewModel, localization);
                using var settingsViewModel = new SettingsViewModel(
                    localization,
                    new AppSettingsService());
                using var coachingViewModel = new CoachingViewModel(
                    CreateCoachPack(),
                    localization,
                    TestGamePatchCatalog.Ready(),
                    TestChampionCatalog.Ready(),
                    TestItemCatalog.Ready());
                var window = new MainWindow(
                    currentMatchViewModel,
                    homeViewModel,
                    settingsViewModel,
                    coachingViewModel);
                window.Show();
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.Loaded);
                Assert.Equal("TempoCue — ассистент для League of Legends", window.Title);
                Assert.Equal(1280, window.Width);
                Assert.Equal(720, window.Height);
                Assert.Equal(1280, window.MinWidth);
                Assert.Equal(720, window.MinHeight);
                Assert.Equal(ResizeMode.CanResize, window.ResizeMode);
                Assert.Equal(WindowStyle.None, window.WindowStyle);
                var windowChrome = WindowChrome.GetWindowChrome(window);
                Assert.NotNull(windowChrome);
                Assert.Equal(new Thickness(6), windowChrome.ResizeBorderThickness);
                Assert.Equal(new Thickness(0), windowChrome.GlassFrameThickness);
                Assert.NotNull(window.Icon);
                window.WindowState = WindowState.Maximized;
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.Render);
                Assert.True(window.ActualWidth <= SystemParameters.WorkArea.Width + 1);
                Assert.True(window.ActualHeight <= SystemParameters.WorkArea.Height + 1);
                window.WindowState = WindowState.Normal;
                var homeNavigationIcon = Assert.IsType<System.Windows.Shapes.Path>(
                    window.FindName("HomeNavigationIcon"));
                Assert.NotNull(homeNavigationIcon.Data);
                Assert.IsType<StreamGeometry>(window.FindResource("Icon.NavHome"));
                var currentMatchPage = Assert.IsType<TempoCue.App.Views.CurrentMatchView>(
                    window.FindName("CurrentMatchContentPage"));
                Assert.Contains(
                    currentMatchPage.Resources.MergedDictionaries,
                    dictionary => dictionary.Contains("ShellScrollBar"));
                var currentMatchScrollBarStyle = Assert.IsType<Style>(
                    currentMatchPage.FindResource(typeof(ScrollBar)));
                Assert.NotNull(currentMatchScrollBarStyle.BasedOn);
                var currentMatchButton = Assert.IsType<Button>(
                    window.FindName("CurrentMatchNavigationButton"));
                var settingsPage = Assert.IsType<TempoCue.App.Views.SettingsView>(
                    window.FindName("SettingsContentPage"));
                var settingsButton = Assert.IsType<Button>(window.FindName("SettingsNavigationButton"));
                var coachingPage = Assert.IsType<TempoCue.App.Views.CoachingView>(
                    window.FindName("CoachingContentPage"));
                var coachingButton = Assert.IsType<Button>(
                    window.FindName("CoachingNavigationButton"));
                var coachingLibraryPanel = Assert.IsType<Grid>(
                    coachingPage.FindName("CoachingLibraryPanel"));
                var packTypeSelectionPage = Assert.IsType<TempoCue.App.Views.PackTypeSelectionView>(
                    coachingPage.FindName("PackTypeSelectionPage"));
                var matchupEditorPage = Assert.IsType<TempoCue.App.Views.MatchupEditorView>(
                    coachingPage.FindName("MatchupEditorPage"));
                var matchupMainInformationPage = Assert.IsType<TempoCue.App.Views.MatchupMainInformationView>(
                    matchupEditorPage.FindName("MatchupMainInformationPage"));
                var matchupChampionsAndRolePage = Assert.IsType<TempoCue.App.Views.MatchupChampionsAndRoleView>(
                    matchupEditorPage.FindName("MatchupChampionsAndRolePage"));
                var matchupContentPage = Assert.IsType<TempoCue.App.Views.MatchupContentView>(
                    matchupEditorPage.FindName("MatchupContentPage"));
                var championPickerDialog = Assert.IsType<TempoCue.App.Views.ChampionPickerDialogView>(
                    matchupEditorPage.FindName("ChampionPickerDialog"));
                var itemPickerDialog = Assert.IsType<TempoCue.App.Views.ItemPickerDialogView>(
                    matchupEditorPage.FindName("ItemPickerDialog"));
                var sharedTextBoxPadding = new Thickness(12, 0, 12, 0);
                foreach (var textBoxName in new[]
                         {
                             "MatchupPackNameTextBox",
                             "MatchupAuthorTextBox"
                         })
                {
                    Assert.Equal(
                        sharedTextBoxPadding,
                        Assert.IsType<TextBox>(matchupMainInformationPage.FindName(textBoxName)).Padding);
                }
                Assert.Equal(
                    36d,
                    Assert.IsType<TextBox>(
                        matchupMainInformationPage.FindName("MatchupPackNameTextBox")).Height);
                Assert.Equal(
                    58d,
                    Assert.IsType<TextBox>(
                        matchupMainInformationPage.FindName("MatchupDescriptionTextBox")).Height);
                Assert.Equal(
                    new Thickness(12, 4, 12, 4),
                    Assert.IsType<TextBox>(
                        matchupMainInformationPage.FindName("MatchupDescriptionTextBox")).Padding);
                var settingsTitle = Assert.IsType<TextBlock>(
                    settingsPage.FindName("SettingsTitleTextBlock"));
                var languageComboBox = Assert.IsType<ComboBox>(
                    settingsPage.FindName("LanguageComboBox"));

                currentMatchButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(Visibility.Visible, currentMatchPage.Visibility);
                window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.Render);
                var currentMatchScrollBar = Assert.Single(
                    FindVisualDescendants<ScrollBar>(currentMatchPage),
                    scrollBar => scrollBar.Orientation == Orientation.Vertical
                        && scrollBar.IsVisible);
                Assert.Equal(4d, currentMatchScrollBar.ActualWidth);
                var currentMatchTabs = Assert.IsType<TabControl>(
                    currentMatchPage.FindName("CurrentSectionsTabControl"));
                Assert.Equal(3, currentMatchTabs.Items.Count);
                Assert.Equal(new Thickness(1), currentMatchTabs.BorderThickness);
                Assert.Equal(306d, currentMatchTabs.MinHeight);

                coachingButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(Visibility.Visible, coachingPage.Visibility);
                window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.Render);
                var coachingTabs = Assert.IsType<TabControl>(
                    coachingPage.FindName("CoachingSectionsTabControl"));
                Assert.Equal(3, coachingTabs.Items.Count);
                var createPackButton = Assert.IsType<Button>(
                    coachingPage.FindName("CreatePackButton"));
                Assert.True(createPackButton.IsEnabled);
                Assert.True(coachingPage.ActualWidth > 900);
                Assert.True(coachingPage.ActualHeight > 500);
                var rulesScrollViewer = Assert.IsType<ScrollViewer>(
                    coachingPage.FindName("CoachingRulesScrollViewer"));
                Assert.Equal(
                    ScrollBarVisibility.Auto,
                    rulesScrollViewer.VerticalScrollBarVisibility);
                var scrollBarStyle = Assert.IsType<Style>(
                    rulesScrollViewer.FindResource("ShellScrollBar"));
                var widthSetter = Assert.Single(
                    scrollBarStyle.Setters.OfType<Setter>(),
                    setter => setter.Property == FrameworkElement.WidthProperty);
                Assert.Equal(4d, widthSetter.Value);
                var ruleCardStyle = Assert.IsType<Style>(
                    coachingPage.FindResource("CoachingRuleCard"));
                var ruleCardMargin = Assert.Single(
                    ruleCardStyle.Setters.OfType<Setter>(),
                    setter => setter.Property == FrameworkElement.MarginProperty);
                Assert.Equal(new Thickness(0, 0, 8, 10), ruleCardMargin.Value);
                var coachingTabStyle = Assert.IsType<Style>(
                    coachingPage.FindResource("CoachingTab"));
                var coachingTabFontSize = Assert.Single(
                    coachingTabStyle.Setters.OfType<Setter>(),
                    setter => setter.Property == Control.FontSizeProperty);
                Assert.Equal(15d, coachingTabFontSize.Value);
                Assert.Equal(
                    24d,
                    Assert.IsType<double>(
                        coachingPage.FindResource("Typography.FontSize.PageTitle")));

                coachingTabs.SelectedIndex = 1;
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.Render);
                Assert.True(Assert.IsType<Border>(
                    coachingPage.FindName("MapControlEmptyState")).IsVisible);

                coachingTabs.SelectedIndex = 2;
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.Render);
                Assert.True(Assert.IsType<Border>(
                    coachingPage.FindName("MyPacksEmptyState")).IsVisible);

                createPackButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.Equal(Visibility.Collapsed, coachingLibraryPanel.Visibility);
                Assert.Equal(Visibility.Visible, packTypeSelectionPage.Visibility);
                var matchupPackTypeButton = Assert.IsType<Button>(
                    packTypeSelectionPage.FindName("MatchupPackTypeButton"));
                var gameControlPackTypeButton = Assert.IsType<Button>(
                    packTypeSelectionPage.FindName("GameControlPackTypeButton"));
                Assert.True(matchupPackTypeButton.IsEnabled);
                Assert.False(gameControlPackTypeButton.IsEnabled);

                matchupPackTypeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.Equal(Visibility.Collapsed, packTypeSelectionPage.Visibility);
                Assert.Equal(Visibility.Visible, matchupEditorPage.Visibility);
                Assert.Equal(
                    24d,
                    Assert.IsType<TextBlock>(
                        matchupEditorPage.FindName("MatchupEditorTitleTextBlock")).FontSize);
                var matchupEditorStepsPanel = Assert.IsType<Border>(
                    matchupEditorPage.FindName("MatchupEditorStepsPanel"));
                Assert.Equal(new CornerRadius(12), matchupEditorStepsPanel.CornerRadius);
                Assert.Equal(new Thickness(12, 10, 8, 8), matchupEditorStepsPanel.Padding);
                var matchupEditorSteps = Assert.IsType<ListBox>(
                    matchupEditorPage.FindName("MatchupEditorStepsListBox"));
                Assert.Equal(5, matchupEditorSteps.Items.Count);
                Assert.Equal(0, matchupEditorSteps.SelectedIndex);
                Assert.True(matchupEditorSteps.IsHitTestVisible);
                Assert.All(
                    matchupEditorSteps.Items.Cast<ListBoxItem>(),
                    item => Assert.False(item.IsHitTestVisible));
                Assert.False(ScrollViewer.GetCanContentScroll(matchupEditorSteps));
                window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.Render);
                var matchupStepsItemsPanel = Assert.Single(
                    FindVisualDescendants<StackPanel>(matchupEditorSteps),
                    panel => panel.IsItemsHost);
                Assert.Equal(new Thickness(0, 0, 0, 8), matchupStepsItemsPanel.Margin);
                var matchupStepsScrollViewer = Assert.Single(
                    FindVisualDescendants<ScrollViewer>(matchupEditorSteps));
                Assert.Equal(0d, matchupStepsScrollViewer.ScrollableWidth);
                Assert.Same(
                    coachingViewModel.MatchupEditor.MainInformation,
                    matchupMainInformationPage.DataContext);
                Assert.Equal(
                    "Основная информация",
                    Assert.IsType<TextBlock>(
                        matchupMainInformationPage.FindName("MatchupEditorStepTitleTextBlock")).Text);
                Assert.Equal(
                    "От 3 до 80 символов.",
                    Assert.IsType<TextBlock>(
                        matchupMainInformationPage.FindName(
                            "MatchupPackNameLengthFeedback")).Text);
                coachingViewModel.MatchupEditor.MainInformation.ShortDescription = "3213123";
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.Equal(
                    "7 из минимум 10 символов. Максимум: 240.",
                    Assert.IsType<TextBlock>(
                        matchupMainInformationPage.FindName(
                            "MatchupDescriptionLengthFeedback")).Text);
                coachingViewModel.MatchupEditor.MainInformation.PackName = "Urgot против Sett";
                coachingViewModel.MatchupEditor.MainInformation.ShortDescription =
                    "Как пережить раннюю линию и найти момент для боя.";
                coachingViewModel.MatchupEditor.MainInformation.Author = "TopLaneAuthor";
                coachingViewModel.MatchupEditor.MainInformation
                    .EnsureGamePatchCatalogLoadedAsync()
                    .GetAwaiter()
                    .GetResult();
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);

                var patchComboBox = Assert.IsType<ComboBox>(
                    matchupMainInformationPage.FindName("MatchupPatchComboBox"));
                Assert.Equal(36d, patchComboBox.Height);
                Assert.Equal("26.15 — текущий", patchComboBox.Text);
                Assert.Equal(
                    "Urgot против Sett",
                    Assert.IsType<TextBlock>(
                        matchupEditorPage.FindName("MatchupEditorTitleTextBlock")).Text);
                Assert.Equal(
                    "О языке набора",
                    Assert.IsType<TextBlock>(
                        matchupMainInformationPage.FindName("MatchupLanguageNoteTitleTextBlock")).Text);
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                var matchupEditorNextButton = Assert.IsType<Button>(
                    matchupEditorPage.FindName("MatchupEditorNextButton"));
                var matchupEditorContentScrollViewer = Assert.IsType<ScrollViewer>(
                    matchupEditorPage.FindName("MatchupEditorContentScrollViewer"));
                matchupEditorContentScrollViewer.ScrollToVerticalOffset(120);
                window.UpdateLayout();
                Assert.True(matchupEditorNextButton.IsEnabled);
                matchupEditorNextButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.ApplicationIdle);

                Assert.Equal(2, coachingViewModel.MatchupEditor.CurrentStep);
                Assert.Equal(0d, matchupEditorContentScrollViewer.VerticalOffset);
                Assert.Equal(1, matchupEditorSteps.SelectedIndex);
                Assert.Equal(Visibility.Collapsed, matchupMainInformationPage.Visibility);
                Assert.Equal(Visibility.Visible, matchupChampionsAndRolePage.Visibility);
                Assert.Equal(
                    "Шаг 2 из 5 · Чемпионы и роль",
                    Assert.IsType<TextBlock>(
                        matchupEditorPage.FindName("MatchupEditorStepSummaryTextBlock")).Text);

                var championsAndRole = coachingViewModel.MatchupEditor.ChampionsAndRole;
                championsAndRole.EnsureCatalogLoadedAsync().GetAwaiter().GetResult();
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.Equal(
                    Visibility.Visible,
                    Assert.IsType<StackPanel>(
                        matchupChampionsAndRolePage.FindName("ChampionSelectionContent")).Visibility);
                Assert.IsType<Button>(
                        matchupChampionsAndRolePage.FindName("ChangePlayerChampionButton"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.Equal(Visibility.Visible, championPickerDialog.Visibility);
                Assert.Equal(ChampionPickerTarget.Player, championsAndRole.PickerTarget);
                window.UpdateLayout();
                var championPickerDialogCard = Assert.IsType<Border>(
                    championPickerDialog.FindName("ChampionPickerDialogCard"));
                var championPickerDialogHeight = Math.Clamp(window.ActualHeight * 0.80, 520, 760);
                var championPickerDialogWidth = Math.Clamp(window.ActualWidth * 0.70, 760, 1000);
                Assert.Equal(championPickerDialogHeight, championPickerDialogCard.ActualHeight, 1);
                Assert.Equal(championPickerDialogWidth, championPickerDialogCard.ActualWidth, 1);
                window.Width = 1400;
                window.Height = 800;
                window.UpdateLayout();
                Assert.Equal(640d, championPickerDialogCard.ActualHeight, 1);
                Assert.Equal(980d, championPickerDialogCard.ActualWidth, 1);
                window.Width = 1280;
                window.Height = 720;
                window.UpdateLayout();
                championsAndRole.PickerSearch = "urgot";
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                window.UpdateLayout();
                Assert.Equal(championPickerDialogHeight, championPickerDialogCard.ActualHeight, 1);
                championsAndRole.PickerSearch = string.Empty;
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                var closePickerButton = Assert.IsType<Button>(
                    championPickerDialog.FindName("CloseChampionPickerButton"));
                Assert.Equal(new Thickness(0), closePickerButton.Padding);
                var closePickerIcon = Assert.Single(
                    FindVisualDescendants<System.Windows.Shapes.Path>(closePickerButton));
                Assert.NotNull(closePickerIcon.Data);
                Assert.NotNull(closePickerIcon.Stroke);
                var pickerSearchTextBox = Assert.IsType<TextBox>(
                    championPickerDialog.FindName("ChampionPickerSearchTextBox"));
                Assert.Equal(sharedTextBoxPadding, pickerSearchTextBox.Padding);
                Assert.Equal(600d, pickerSearchTextBox.ActualWidth);
                var pickerSearchPlaceholder = Assert.IsType<TextBlock>(
                    championPickerDialog.FindName("ChampionPickerSearchPlaceholder"));
                Assert.Equal(new Thickness(9, 0, 0, 0), pickerSearchPlaceholder.Margin);
                var pickerItems = Assert.IsType<ItemsControl>(
                    championPickerDialog.FindName("ChampionPickerItemsControl"));
                var pickerButtonStyle = Assert.IsType<Style>(
                    championPickerDialog.FindResource("ChampionPickerButton"));
                var pickerButtonWidth = Assert.IsType<double>(Assert.Single(
                    pickerButtonStyle.Setters.OfType<Setter>(),
                    setter => setter.Property == FrameworkElement.WidthProperty).Value);
                var pickerButtonMargin = Assert.IsType<Thickness>(Assert.Single(
                    pickerButtonStyle.Setters.OfType<Setter>(),
                    setter => setter.Property == FrameworkElement.MarginProperty).Value);
                var fiveCardRowWidth = (pickerButtonWidth * 5) + (pickerButtonMargin.Right * 4);
                Assert.Equal(fiveCardRowWidth, pickerSearchTextBox.ActualWidth);
                pickerSearchTextBox.Focus();
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.Input);
                Assert.True(pickerSearchTextBox.IsKeyboardFocusWithin);
                Assert.Equal(
                    Visibility.Collapsed,
                    pickerSearchPlaceholder.Visibility);
                var urgotButton = FindVisualDescendants<Button>(pickerItems)
                    .Single(button => button.DataContext is ChampionOptionViewModel { Id: "Urgot" });
                urgotButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.Equal(Visibility.Collapsed, championPickerDialog.Visibility);

                Assert.IsType<Button>(
                        matchupChampionsAndRolePage.FindName("ChangeOpponentChampionButton"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                window.UpdateLayout();
                var settButton = FindVisualDescendants<Button>(pickerItems)
                    .Single(button => button.DataContext is ChampionOptionViewModel { Id: "Sett" });
                settButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.True(championsAndRole.IsComplete);
                Assert.True(matchupEditorNextButton.IsEnabled);
                matchupEditorNextButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.Equal(3, coachingViewModel.MatchupEditor.CurrentStep);
                Assert.Equal(Visibility.Collapsed, matchupChampionsAndRolePage.Visibility);
                Assert.Equal(Visibility.Visible, matchupContentPage.Visibility);
                Assert.Equal(
                    "Шаг 3 из 5 · Содержание матчапа",
                    Assert.IsType<TextBlock>(
                        matchupEditorPage.FindName("MatchupEditorStepSummaryTextBlock")).Text);

                var content = coachingViewModel.MatchupEditor.Content;
                content.EnsureItemCatalogLoadedAsync().GetAwaiter().GetResult();
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);

                var addPhaseButton = Assert.IsType<Button>(
                    matchupContentPage.FindName("AddPhaseButton"));
                addPhaseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.Equal(
                    Visibility.Visible,
                    Assert.IsType<Border>(matchupContentPage.FindName("PhaseEditorPanel")).Visibility);
                Assert.Equal(
                    "Фаза активна на уровнях 1+",
                    Assert.IsType<TextBlock>(
                        matchupContentPage.FindName("PhaseMeaningSummaryTextBlock")).Text);
                Assert.Equal(
                    "От 3 до 80 символов.",
                    Assert.IsType<TextBlock>(
                        matchupContentPage.FindName("PhaseNameLengthFeedback")).Text);
                Assert.Equal(
                    "От 3 до 200 символов.",
                    Assert.IsType<TextBlock>(
                        matchupContentPage.FindName("PhasePrimaryAdviceLengthFeedback")).Text);
                var selectStartItemButton = Assert.IsType<Button>(
                    matchupContentPage.FindName("SelectStartItemButton"));
                Assert.True(content.ItemPicker.IsCatalogReady);
                selectStartItemButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.Equal(Visibility.Visible, itemPickerDialog.Visibility);
                window.UpdateLayout();
                var itemPickerDialogCard = Assert.IsType<Border>(
                    itemPickerDialog.FindName("ItemPickerDialogCard"));
                var itemPickerDialogHeight = Math.Clamp(window.ActualHeight * 0.80, 520, 760);
                var itemPickerDialogWidth = Math.Clamp(window.ActualWidth * 0.78, 880, 1180);
                Assert.Equal(itemPickerDialogHeight, itemPickerDialogCard.ActualHeight, 1);
                Assert.Equal(itemPickerDialogWidth, itemPickerDialogCard.ActualWidth, 1);
                window.Width = 1400;
                window.Height = 800;
                window.UpdateLayout();
                Assert.Equal(640d, itemPickerDialogCard.ActualHeight, 1);
                Assert.Equal(1092d, itemPickerDialogCard.ActualWidth, 1);
                window.Width = 1280;
                window.Height = 720;
                window.UpdateLayout();
                content.ItemPicker.Search = "секира";
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                window.UpdateLayout();
                Assert.Equal(itemPickerDialogHeight, itemPickerDialogCard.ActualHeight, 1);
                content.ItemPicker.Search = string.Empty;
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                var itemPickerSearchTextBox = Assert.IsType<TextBox>(
                    itemPickerDialog.FindName("ItemPickerSearchTextBox"));
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.Input);
                Assert.Equal(sharedTextBoxPadding, itemPickerSearchTextBox.Padding);
                Assert.True(itemPickerSearchTextBox.IsKeyboardFocusWithin);
                Assert.Equal(
                    Visibility.Collapsed,
                    Assert.IsType<TextBlock>(
                        itemPickerDialog.FindName("ItemPickerSearchPlaceholder")).Visibility);
                var itemPickerItems = Assert.IsType<ItemsControl>(
                    itemPickerDialog.FindName("ItemPickerItemsControl"));
                Assert.Equal(
                    6,
                    Assert.IsType<ItemsControl>(
                        itemPickerDialog.FindName("ItemCategoryItemsControl")).Items.Count);
                var itemSortComboBox = Assert.IsType<ComboBox>(
                    itemPickerDialog.FindName("ItemSortComboBox"));
                Assert.Equal(3, itemSortComboBox.Items.Count);
                Assert.Equal("По названию", itemSortComboBox.Text);
                window.UpdateLayout();
                var cleaverButton = FindVisualDescendants<Button>(itemPickerItems)
                    .Single(button => button.DataContext is ItemOptionViewModel { Id: 3071 });
                cleaverButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.Equal(Visibility.Visible, itemPickerDialog.Visibility);
                Assert.Equal(3071, content.ItemPicker.SelectedItem?.Id);
                var confirmItemSelectionButton = Assert.IsType<Button>(
                    itemPickerDialog.FindName("ConfirmItemSelectionButton"));
                Assert.True(confirmItemSelectionButton.IsEnabled);
                confirmItemSelectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.Equal(Visibility.Collapsed, itemPickerDialog.Visibility);
                Assert.Equal(3071, content.Editor.StartItem?.Id);
                foreach (var inputName in new[]
                         {
                             "PhasePrimaryAdviceTextBox",
                             "PhaseWhyNowTextBox",
                             "PhaseNextGoalTextBox"
                         })
                {
                    var input = Assert.IsType<TextBox>(matchupContentPage.FindName(inputName));
                    Assert.Equal(36, input.Height);
                    Assert.False(input.AcceptsReturn);
                }
                content.Editor.Name = "Осторожное начало";
                content.Editor.PrimaryAdvice = "Не ищи размен первым";
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                var savePhaseButton = Assert.IsType<Button>(
                    matchupContentPage.FindName("SavePhaseButton"));
                Assert.True(savePhaseButton.IsEnabled);
                savePhaseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.Single(content.Phases);
                Assert.Equal("Фазы 1", Assert.IsType<Button>(
                    matchupContentPage.FindName("MatchupPhasesTabButton")).Content);

                Assert.IsType<Button>(
                        matchupEditorPage.FindName("BackToPackTypeButton"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.Equal(2, coachingViewModel.MatchupEditor.CurrentStep);
                Assert.Equal(Visibility.Visible, matchupChampionsAndRolePage.Visibility);
                Assert.Equal(Visibility.Collapsed, matchupContentPage.Visibility);
                Assert.Single(content.Phases);
                Assert.IsType<Button>(
                        matchupEditorPage.FindName("BackToPackTypeButton"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.Equal(1, coachingViewModel.MatchupEditor.CurrentStep);
                Assert.Equal(Visibility.Visible, matchupMainInformationPage.Visibility);
                Assert.Equal(Visibility.Collapsed, matchupChampionsAndRolePage.Visibility);
                Assert.False(Assert.IsType<Button>(
                    matchupEditorPage.FindName("BackToPackTypeButton")).IsEnabled);

                coachingViewModel.OpenPackTypeSelection();
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.Equal(Visibility.Visible, packTypeSelectionPage.Visibility);

                Assert.IsType<Button>(
                        packTypeSelectionPage.FindName("BackToLibraryButton"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.Equal(Visibility.Visible, coachingLibraryPanel.Visibility);

                settingsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(Visibility.Visible, settingsPage.Visibility);
                Assert.Equal("Настройки", settingsTitle.Text);
                window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.Render);
                Assert.Contains(
                    FindVisualDescendants<TextBlock>(languageComboBox),
                    textBlock => textBlock.Text == settingsViewModel.Languages[0].DisplayName);

                localization.SetLanguage("en-US");
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                Assert.Equal("26.15 — current", patchComboBox.Text);

                Assert.Equal("TempoCue — League of Legends assistant", window.Title);
                Assert.Equal("Settings", settingsTitle.Text);
                Assert.Equal(
                    "What kind of advice pack are we creating?",
                    Assert.IsType<TextBlock>(
                        packTypeSelectionPage.FindName("PackTypeTitleTextBlock")).Text);
                Assert.Equal(
                    "Champions and role",
                    Assert.IsType<TextBlock>(
                        matchupChampionsAndRolePage.FindName("MatchupChampionsStepTitleTextBlock")).Text);
                championsAndRole.EnsureCatalogLoadedAsync().GetAwaiter().GetResult();
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.DataBind);
                window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.Render);
                Assert.Contains(
                    FindVisualDescendants<TextBlock>(languageComboBox),
                    textBlock => textBlock.Text == settingsViewModel.Languages[1].DisplayName);
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
            Assert.Equal("Coaching", localization.Get("Coaching.Title"));
            Assert.Equal("Map control", localization.Get("Coaching.Tab.MapControl"));
            Assert.Equal("Interface language", localization.Get("Settings.Language.Label"));
            Assert.Equal("Available now", localization.Get("Home.Available.Title"));
            Assert.Equal("v0.2.0", AppVersion.Display);
            Assert.Equal("1 player", localization.Plural("Count.Player", 1));
            Assert.Equal("10 players", localization.Plural("Count.Player", 10));

            localization.SetLanguage("ru-RU");

            Assert.Equal("Симулятор игрового состояния", localization.Get("Header.Title"));
            Assert.Equal("Главная", localization.Get("Nav.Home"));
            Assert.Equal("Настройки", localization.Get("Settings.Title"));
            Assert.Equal("Коучинг", localization.Get("Coaching.Title"));
            Assert.Equal("Контроль карты", localization.Get("Coaching.Tab.MapControl"));
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

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

}

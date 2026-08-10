using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Tests;

/// <summary>Проверяет наблюдаемое поведение отдельных шагов редактора матчапа.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MatchupEditorViewTests
{
    private static readonly Thickness SharedTextBoxPadding = new(12, 0, 12, 0);

    [Fact]
    public void MainInformation_ExplainsValidationAndOpensChampionStep() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        context.Show();
        context.OpenMatchupEditor();
        var page = context.MainInformationPage;
        var editorPage = context.MatchupEditorPage;
        var viewModel = context.Coaching.MatchupEditor.MainInformation;

        Assert.Same(viewModel, page.DataContext);
        Assert.Equal(
            "Основная информация",
            WpfTestHost.Find<TextBlock>(page, "MatchupEditorStepTitleTextBlock").Text);
        foreach (var textBoxName in new[] { "MatchupPackNameTextBox", "MatchupAuthorTextBox" })
        {
            Assert.Equal(
                SharedTextBoxPadding,
                WpfTestHost.Find<TextBox>(page, textBoxName).Padding);
        }

        Assert.Equal(36d, WpfTestHost.Find<TextBox>(page, "MatchupPackNameTextBox").Height);
        Assert.Equal(58d, WpfTestHost.Find<TextBox>(page, "MatchupDescriptionTextBox").Height);
        Assert.Equal(
            new Thickness(12, 4, 12, 4),
            WpfTestHost.Find<TextBox>(page, "MatchupDescriptionTextBox").Padding);
        Assert.Equal(
            "От 3 до 80 символов.",
            WpfTestHost.Find<TextBlock>(page, "MatchupPackNameLengthFeedback").Text);

        viewModel.ShortDescription = "3213123";
        WpfTestHost.Pump();
        Assert.Equal(
            "7 из минимум 10 символов. Максимум: 240.",
            WpfTestHost.Find<TextBlock>(page, "MatchupDescriptionLengthFeedback").Text);

        viewModel.PackName = "Urgot против Sett";
        viewModel.ShortDescription = "Как пережить раннюю линию и найти момент для боя.";
        viewModel.Author = "TopLaneAuthor";
        viewModel.EnsureGamePatchCatalogLoadedAsync().GetAwaiter().GetResult();
        WpfTestHost.Pump();
        var patchComboBox = WpfTestHost.Find<ComboBox>(page, "MatchupPatchComboBox");
        Assert.Equal(36d, patchComboBox.Height);
        Assert.Equal("26.15 — текущий", patchComboBox.Text);
        Assert.Equal(
            "Urgot против Sett",
            WpfTestHost.Find<TextBlock>(editorPage, "MatchupEditorTitleTextBlock").Text);
        Assert.Equal(
            "О языке набора",
            WpfTestHost.Find<TextBlock>(page, "MatchupLanguageNoteTitleTextBlock").Text);

        var nextButton = WpfTestHost.Find<Button>(editorPage, "MatchupEditorNextButton");
        var contentScrollViewer = WpfTestHost.Find<ScrollViewer>(
            editorPage,
            "MatchupEditorContentScrollViewer");
        contentScrollViewer.ScrollToVerticalOffset(120);
        context.Window.UpdateLayout();
        Assert.True(nextButton.IsEnabled);
        WpfTestHost.Click(nextButton);
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        Assert.Equal(2, context.Coaching.MatchupEditor.CurrentStep);
        Assert.Equal(0d, contentScrollViewer.VerticalOffset);
        Assert.Equal(
            1,
            WpfTestHost.Find<ListBox>(editorPage, "MatchupEditorStepsListBox").SelectedIndex);
        Assert.Equal(Visibility.Collapsed, page.Visibility);
        Assert.Equal(Visibility.Visible, context.ChampionsAndRolePage.Visibility);
        Assert.Equal(
            "Шаг 2 из 5 · Чемпионы и роль",
            WpfTestHost.Find<TextBlock>(editorPage, "MatchupEditorStepSummaryTextBlock").Text);
    });

    [Fact]
    public void ChampionPicker_SelectsBothSidesAndKeepsResponsiveLayout() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        context.Show();
        context.OpenMatchupEditor();
        context.CompleteMainInformationAndOpenChampions();
        var page = context.ChampionsAndRolePage;
        var picker = context.ChampionPickerDialog;
        var viewModel = context.Coaching.MatchupEditor.ChampionsAndRole;
        viewModel.EnsureCatalogLoadedAsync().GetAwaiter().GetResult();
        WpfTestHost.Pump();

        Assert.Equal(
            Visibility.Visible,
            WpfTestHost.Find<StackPanel>(page, "ChampionSelectionContent").Visibility);
        WpfTestHost.Click(WpfTestHost.Find<Button>(page, "ChangePlayerChampionButton"));
        Assert.Equal(Visibility.Visible, picker.Visibility);
        Assert.Equal(ChampionPickerTarget.Player, viewModel.PickerTarget);
        context.Window.UpdateLayout();
        var card = WpfTestHost.Find<Border>(picker, "ChampionPickerDialogCard");
        Assert.Equal(Math.Clamp(context.Window.ActualHeight * 0.80, 520, 760), card.ActualHeight, 1);
        Assert.Equal(Math.Clamp(context.Window.ActualWidth * 0.70, 760, 1000), card.ActualWidth, 1);
        context.Window.Width = 1400;
        context.Window.Height = 800;
        context.Window.UpdateLayout();
        Assert.Equal(640d, card.ActualHeight, 1);
        Assert.Equal(980d, card.ActualWidth, 1);
        context.Window.Width = 1280;
        context.Window.Height = 720;
        context.Window.UpdateLayout();

        viewModel.PickerSearch = "urgot";
        WpfTestHost.Pump();
        context.Window.UpdateLayout();
        Assert.Equal(576d, card.ActualHeight, 1);
        viewModel.PickerSearch = string.Empty;
        WpfTestHost.Pump();
        var closeButton = WpfTestHost.Find<Button>(picker, "CloseChampionPickerButton");
        Assert.Equal(new Thickness(0), closeButton.Padding);
        var closeIcon = Assert.Single(
            WpfTestHost.FindVisualDescendants<System.Windows.Shapes.Path>(closeButton));
        Assert.NotNull(closeIcon.Data);
        Assert.NotNull(closeIcon.Stroke);
        var searchTextBox = WpfTestHost.Find<TextBox>(picker, "ChampionPickerSearchTextBox");
        Assert.Equal(SharedTextBoxPadding, searchTextBox.Padding);
        Assert.Equal(600d, searchTextBox.ActualWidth);
        Assert.Equal(
            new Thickness(9, 0, 0, 0),
            WpfTestHost.Find<TextBlock>(picker, "ChampionPickerSearchPlaceholder").Margin);
        var pickerItems = WpfTestHost.Find<ItemsControl>(picker, "ChampionPickerItemsControl");
        var pickerButtonStyle = Assert.IsType<Style>(picker.FindResource("ChampionPickerButton"));
        var pickerButtonWidth = Assert.IsType<double>(Assert.Single(
            pickerButtonStyle.Setters.OfType<Setter>(),
            setter => setter.Property == FrameworkElement.WidthProperty).Value);
        var pickerButtonMargin = Assert.IsType<Thickness>(Assert.Single(
            pickerButtonStyle.Setters.OfType<Setter>(),
            setter => setter.Property == FrameworkElement.MarginProperty).Value);
        Assert.Equal(
            (pickerButtonWidth * 5) + (pickerButtonMargin.Right * 4),
            searchTextBox.ActualWidth);
        searchTextBox.Focus();
        WpfTestHost.Pump(DispatcherPriority.Input);
        Assert.True(searchTextBox.IsKeyboardFocusWithin);
        Assert.Equal(
            Visibility.Collapsed,
            WpfTestHost.Find<TextBlock>(picker, "ChampionPickerSearchPlaceholder").Visibility);

        var urgotButton = WpfTestHost.FindVisualDescendants<Button>(pickerItems)
            .Single(button => button.DataContext is ChampionOptionViewModel { Id: "Urgot" });
        WpfTestHost.Click(urgotButton);
        Assert.Equal(Visibility.Collapsed, picker.Visibility);
        WpfTestHost.Click(WpfTestHost.Find<Button>(page, "ChangeOpponentChampionButton"));
        context.Window.UpdateLayout();
        var settButton = WpfTestHost.FindVisualDescendants<Button>(pickerItems)
            .Single(button => button.DataContext is ChampionOptionViewModel { Id: "Sett" });
        WpfTestHost.Click(settButton);
        Assert.True(viewModel.IsComplete);

        var nextButton = WpfTestHost.Find<Button>(context.MatchupEditorPage, "MatchupEditorNextButton");
        Assert.True(nextButton.IsEnabled);
        WpfTestHost.Click(nextButton);
        Assert.Equal(3, context.Coaching.MatchupEditor.CurrentStep);
        Assert.Equal(Visibility.Collapsed, page.Visibility);
        Assert.Equal(Visibility.Visible, context.ContentPage.Visibility);
    });

    [Fact]
    public void PhaseEditor_SelectsItemSavesPhaseAndPreservesDraftSteps() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        context.Show();
        context.OpenMatchupEditor();
        context.CompleteMainInformationAndOpenChampions();
        context.SelectChampionsAndOpenContent();
        var page = context.ContentPage;
        var phaseEditor = context.PhaseEditorView;
        var itemConditions = context.PhaseItemConditionsView;
        var phaseList = context.PhaseListView;
        var editorPage = context.MatchupEditorPage;
        var content = context.Coaching.MatchupEditor.Content;
        content.EnsureItemCatalogLoadedAsync().GetAwaiter().GetResult();
        WpfTestHost.Pump();

        WpfTestHost.Click(WpfTestHost.Find<Button>(phaseList, "AddPhaseButton"));
        Assert.Equal(
            Visibility.Visible,
            WpfTestHost.Find<Border>(phaseEditor, "PhaseEditorPanel").Visibility);
        Assert.Equal(
            "Фаза активна на уровнях 1+",
            WpfTestHost.Find<TextBlock>(phaseEditor, "PhaseMeaningSummaryTextBlock").Text);
        Assert.Equal(
            "От 3 до 80 символов.",
            WpfTestHost.Find<TextBlock>(phaseEditor, "PhaseNameLengthFeedback").Text);
        Assert.Equal(
            "От 3 до 200 символов.",
            WpfTestHost.Find<TextBlock>(phaseEditor, "PhasePrimaryAdviceLengthFeedback").Text);

        Assert.True(content.ItemPicker.IsCatalogReady);
        WpfTestHost.Click(WpfTestHost.Find<Button>(itemConditions, "SelectStartItemButton"));
        var picker = context.ItemPickerDialog;
        Assert.Equal(Visibility.Visible, picker.Visibility);
        context.Window.UpdateLayout();
        var card = WpfTestHost.Find<Border>(picker, "ItemPickerDialogCard");
        Assert.Equal(Math.Clamp(context.Window.ActualHeight * 0.80, 520, 760), card.ActualHeight, 1);
        Assert.Equal(Math.Clamp(context.Window.ActualWidth * 0.78, 880, 1180), card.ActualWidth, 1);
        context.Window.Width = 1400;
        context.Window.Height = 800;
        context.Window.UpdateLayout();
        Assert.Equal(640d, card.ActualHeight, 1);
        Assert.Equal(1092d, card.ActualWidth, 1);
        context.Window.Width = 1280;
        context.Window.Height = 720;
        context.Window.UpdateLayout();

        content.ItemPicker.Search = "секира";
        WpfTestHost.Pump();
        context.Window.UpdateLayout();
        Assert.Equal(576d, card.ActualHeight, 1);
        content.ItemPicker.Search = string.Empty;
        WpfTestHost.Pump();
        var searchTextBox = WpfTestHost.Find<TextBox>(picker, "ItemPickerSearchTextBox");
        WpfTestHost.Pump(DispatcherPriority.Input);
        Assert.Equal(SharedTextBoxPadding, searchTextBox.Padding);
        Assert.True(searchTextBox.IsKeyboardFocusWithin);
        Assert.Equal(
            Visibility.Collapsed,
            WpfTestHost.Find<TextBlock>(picker, "ItemPickerSearchPlaceholder").Visibility);
        Assert.Equal(
            6,
            WpfTestHost.Find<ItemsControl>(picker, "ItemCategoryItemsControl").Items.Count);
        var sortComboBox = WpfTestHost.Find<ComboBox>(picker, "ItemSortComboBox");
        Assert.Equal(3, sortComboBox.Items.Count);
        Assert.Equal("По названию", sortComboBox.Text);

        var pickerItems = WpfTestHost.Find<ItemsControl>(picker, "ItemPickerItemsControl");
        context.Window.UpdateLayout();
        var cleaverButton = WpfTestHost.FindVisualDescendants<Button>(pickerItems)
            .Single(button => button.DataContext is ItemOptionViewModel { Id: 3071 });
        WpfTestHost.Click(cleaverButton);
        Assert.Equal(Visibility.Visible, picker.Visibility);
        Assert.Equal(3071, content.ItemPicker.SelectedItem?.Id);
        var confirmButton = WpfTestHost.Find<Button>(picker, "ConfirmItemSelectionButton");
        Assert.True(confirmButton.IsEnabled);
        WpfTestHost.Click(confirmButton);
        Assert.Equal(Visibility.Collapsed, picker.Visibility);
        Assert.Equal(3071, content.Editor.StartItem?.Id);

        foreach (var inputName in new[]
                 {
                     "PhasePrimaryAdviceTextBox",
                     "PhaseWhyNowTextBox",
                     "PhaseNextGoalTextBox"
                 })
        {
            var input = WpfTestHost.Find<TextBox>(phaseEditor, inputName);
            Assert.Equal(36, input.Height);
            Assert.False(input.AcceptsReturn);
        }

        content.Editor.Name = "Осторожное начало";
        content.Editor.PrimaryAdvice = "Не ищи размен первым";
        WpfTestHost.Pump();
        var saveButton = WpfTestHost.Find<Button>(phaseEditor, "SavePhaseButton");
        Assert.True(saveButton.IsEnabled);
        WpfTestHost.Click(saveButton);
        Assert.Single(content.Phases);
        Assert.Equal(
            "Фазы 1",
            WpfTestHost.Find<Button>(page, "MatchupPhasesTabButton").Content);

        var backButton = WpfTestHost.Find<Button>(editorPage, "BackToPackTypeButton");
        WpfTestHost.Click(backButton);
        Assert.Equal(2, context.Coaching.MatchupEditor.CurrentStep);
        Assert.Equal(Visibility.Visible, context.ChampionsAndRolePage.Visibility);
        Assert.Equal(Visibility.Collapsed, page.Visibility);
        Assert.Single(content.Phases);
        WpfTestHost.Click(backButton);
        Assert.Equal(1, context.Coaching.MatchupEditor.CurrentStep);
        Assert.Equal(Visibility.Visible, context.MainInformationPage.Visibility);
        Assert.Equal(Visibility.Collapsed, context.ChampionsAndRolePage.Visibility);
        Assert.False(backButton.IsEnabled);
    });
}

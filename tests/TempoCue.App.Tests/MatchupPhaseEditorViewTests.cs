using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Tests;

/// <summary>Проверяет редактор одной фазы матчапа и выбор рекомендуемых предметов.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MatchupPhaseEditorViewTests
{
    [Fact]
    public void SelectsItemsSavesPhaseAndPreservesDraftSteps() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        context.Show();
        context.OpenMatchupContent();
        var page = context.ContentPage;
        var phaseEditor = context.PhaseEditorView;
        var itemRecommendations = context.PhaseItemRecommendationsView;
        var phaseList = context.PhaseListView;
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
        WpfTestHost.Click(WpfTestHost.Find<Button>(
            itemRecommendations,
            "SelectRecommendedItemsButton"));
        var picker = context.ItemPickerDialog;
        Assert.Equal(Visibility.Visible, picker.Visibility);
        context.Window.UpdateLayout();
        var card = WpfTestHost.Find<Border>(picker, "ItemPickerDialogCard");
        Assert.Equal(
            EditorUiTestMetrics.CalculateResponsiveSize(
                context.Window.ActualHeight,
                0.80,
                520,
                760),
            card.ActualHeight,
            1);
        Assert.Equal(
            EditorUiTestMetrics.CalculateResponsiveSize(
                context.Window.ActualWidth,
                0.78,
                880,
                1180),
            card.ActualWidth,
            1);
        context.Window.Width = 1400;
        context.Window.Height = 800;
        context.Window.UpdateLayout();
        Assert.Equal(
            EditorUiTestMetrics.CalculateResponsiveSize(
                context.Window.ActualHeight,
                0.80,
                520,
                760),
            card.ActualHeight,
            1);
        Assert.Equal(
            EditorUiTestMetrics.CalculateResponsiveSize(
                context.Window.ActualWidth,
                0.78,
                880,
                1180),
            card.ActualWidth,
            1);
        context.Window.Width = 1310;
        context.Window.Height = 750;
        context.Window.UpdateLayout();

        content.ItemPicker.Search = "секира";
        WpfTestHost.Pump();
        context.Window.UpdateLayout();
        Assert.Equal(
            EditorUiTestMetrics.CalculateResponsiveSize(
                context.Window.ActualHeight,
                0.80,
                520,
                760),
            card.ActualHeight,
            1);
        content.ItemPicker.Search = string.Empty;
        WpfTestHost.Pump();
        var searchTextBox = WpfTestHost.Find<TextBox>(picker, "ItemPickerSearchTextBox");
        WpfTestHost.Pump(DispatcherPriority.Input);
        Assert.Equal(EditorUiTestMetrics.SharedTextBoxPadding, searchTextBox.Padding);
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
        var bootsButton = WpfTestHost.FindVisualDescendants<Button>(pickerItems)
            .Single(button => button.DataContext is ItemOptionViewModel { Id: 3047 });
        WpfTestHost.Click(cleaverButton);
        WpfTestHost.Click(bootsButton);
        Assert.Equal(Visibility.Visible, picker.Visibility);
        Assert.Equal(3047, content.ItemPicker.PreviewedItem?.Id);
        Assert.Equal([3071, 3047], content.ItemPicker.SelectedItems.Select(item => item.Id));
        var confirmButton = WpfTestHost.Find<Button>(picker, "ConfirmItemSelectionButton");
        Assert.True(confirmButton.IsEnabled);
        Assert.Equal("Применить · 2", confirmButton.Content);
        WpfTestHost.Click(confirmButton);
        Assert.Equal(Visibility.Collapsed, picker.Visibility);
        Assert.Equal([3071, 3047], content.Editor.RecommendedItems.Select(item => item.Id));
        Assert.Equal(
            2,
            WpfTestHost.Find<ItemsControl>(
                itemRecommendations,
                "RecommendedItemsControl").Items.Count);

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
    });
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TempoCue.App.Tests;

/// <summary>Проверяет основную информацию и навигацию редактора матчапа.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MatchupEditorViewTests
{
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
                EditorUiTestMetrics.SharedTextBoxPadding,
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
}

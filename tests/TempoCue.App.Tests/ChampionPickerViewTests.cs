using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Tests;

/// <summary>Проверяет модальный выбор чемпионов в редакторе матчапа.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class ChampionPickerViewTests
{
    [Fact]
    public void SelectsBothSidesAndKeepsResponsiveLayout() => WpfTestHost.Run(() =>
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
                0.70,
                760,
                1000),
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
                0.70,
                760,
                1000),
            card.ActualWidth,
            1);
        context.Window.Width = 1310;
        context.Window.Height = 750;
        context.Window.UpdateLayout();

        viewModel.PickerSearch = "urgot";
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
        viewModel.PickerSearch = string.Empty;
        WpfTestHost.Pump();
        var closeButton = WpfTestHost.Find<Button>(picker, "CloseChampionPickerButton");
        Assert.Equal(new Thickness(0), closeButton.Padding);
        var closeIcon = Assert.Single(
            WpfTestHost.FindVisualDescendants<System.Windows.Shapes.Path>(closeButton));
        Assert.NotNull(closeIcon.Data);
        Assert.NotNull(closeIcon.Stroke);
        var searchTextBox = WpfTestHost.Find<TextBox>(picker, "ChampionPickerSearchTextBox");
        Assert.Equal(EditorUiTestMetrics.SharedTextBoxPadding, searchTextBox.Padding);
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
}

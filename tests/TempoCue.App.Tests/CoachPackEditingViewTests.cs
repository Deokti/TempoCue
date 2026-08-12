using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using TempoCue.App.ViewModels;
using TempoCue.App.Views;
using TempoCue.Application;
using static TempoCue.App.Tests.CoachingViewTestContext;

namespace TempoCue.App.Tests;

/// <summary>Проверяет кнопки редактирования и копирования на экране деталей.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class CoachPackEditingViewTests
{
    [Fact]
    public void OwnPack_EditButtonOpensEditorAndCloseReturnsToDetailsFromAnyStep() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        var pack = TestCoachPackFactory.CreateDetailedMatchup();
        context.CoachPackStore.SaveAsync(pack, LocalCoachPackState.Enabled)
            .GetAwaiter()
            .GetResult();
        var library = OpenLibrary(context);
        context.Coaching.Library.RefreshAsync().GetAwaiter().GetResult();
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
        OpenPack(library, pack.Id);

        var editButton = WpfTestHost.Find<Button>(
            context.CoachingPackDetailsPage,
            "PackEditActionButton");
        var stateToggle = WpfTestHost.Find<ToggleButton>(
            context.CoachingPackDetailsPage,
            "PackEnabledToggle");
        Assert.True(stateToggle.IsChecked);
        Assert.Equal(38, stateToggle.ActualWidth);
        Assert.Equal(20, stateToggle.ActualHeight);
        Assert.Equal("Редактировать", editButton.Content);
        var actionsPopup = OpenActionsPopup(context.CoachingPackDetailsPage);
        Assert.Equal(
            Visibility.Visible,
            FindPopupButton(actionsPopup, "DeleteMenuButton").Visibility);
        actionsPopup.IsOpen = false;
        WpfTestHost.Click(editButton);
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        Assert.Equal(Visibility.Visible, context.MatchupEditorPage.Visibility);
        Assert.Equal(MatchupEditorMode.Edit, context.Coaching.MatchupEditor.Mode);
        var steps = WpfTestHost.Find<ListBox>(
            context.MatchupEditorPage,
            "MatchupEditorStepsListBox");
        Assert.All(
            steps.Items.Cast<ListBoxItem>(),
            step => Assert.True(step.IsEnabled));
        WpfTestHost.Click((ListBoxItem)steps.Items[4]);
        Assert.Equal(5, context.Coaching.MatchupEditor.CurrentStep);
        Assert.Equal(Visibility.Visible, context.SavePage.Visibility);
        var closeButton = WpfTestHost.Find<Button>(
            context.MatchupEditorPage,
            "CloseMatchupEditorButton");
        Assert.Equal(Visibility.Visible, closeButton.Visibility);
        SaveVisualSnapshotIfRequested(
            context.Window,
            "matchup-editor-close-action.png");
        WpfTestHost.Click(closeButton);
        Assert.Equal(Visibility.Visible, context.CoachingPackDetailsPage.Visibility);
    });

    [Fact]
    public void BuiltInPack_OffersIndependentCopy() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        var library = OpenLibrary(context);
        var builtIn = Assert.Single(context.Coaching.Library.MatchupCards);
        OpenPack(library, builtIn.Pack.Id);

        var editButton = WpfTestHost.Find<Button>(
            context.CoachingPackDetailsPage,
            "PackEditActionButton");
        var actionsPopup = OpenActionsPopup(context.CoachingPackDetailsPage);
        Assert.Equal(
            Visibility.Collapsed,
            FindPopupButton(actionsPopup, "DeleteMenuButton").Visibility);
        actionsPopup.IsOpen = false;
        Assert.Equal("Создать свою копию", editButton.Content);
        SaveVisualSnapshotIfRequested(
            context.Window,
            "built-in-pack-details.png");
        WpfTestHost.Click(editButton);
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        Assert.Equal(Visibility.Visible, context.MatchupEditorPage.Visibility);
        Assert.Equal(MatchupEditorMode.Copy, context.Coaching.MatchupEditor.Mode);
    });

    [Fact]
    public void OwnPack_DeleteMenuOpensConfirmationAndCancelKeepsDetails() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        var pack = TestCoachPackFactory.CreateDetailedMatchup() with
        {
            Id = "matchup.local.confirm-delete"
        };
        context.CoachPackStore.SaveAsync(pack, LocalCoachPackState.Enabled)
            .GetAwaiter()
            .GetResult();
        var library = OpenLibrary(context);
        context.Coaching.Library.RefreshAsync().GetAwaiter().GetResult();
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
        OpenPack(library, pack.Id);

        var actionsPopup = OpenActionsPopup(context.CoachingPackDetailsPage);
        WpfTestHost.Click(FindPopupButton(actionsPopup, "DeleteMenuButton"));
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        Assert.True(context.Coaching.IsDeleteConfirmationOpen);
        Assert.Equal(
            Visibility.Visible,
            WpfTestHost.Find<Grid>(context.CoachingPage, "DeleteConfirmationOverlay").Visibility);
        Assert.Equal(
            "Удалить навсегда",
            WpfTestHost.Find<Button>(
                context.CoachingPage,
                "ConfirmDeleteCoachPackButton").Content);
        SaveVisualSnapshotIfRequested(context.Window, "coach-pack-delete-confirmation.png");

        WpfTestHost.Click(WpfTestHost.Find<Button>(
            context.CoachingPage,
            "CancelDeleteCoachPackButton"));
        Assert.False(context.Coaching.IsDeleteConfirmationOpen);
        Assert.Equal(CoachingScreen.PackDetails, context.Coaching.CurrentScreen);
    });

    [Fact]
    public void ImportedPack_UsesCopyAndRemoveInstallationActions() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        var pack = TestCoachPackFactory.CreateDetailedMatchup() with
        {
            Id = "matchup.imported.actions"
        };
        context.CoachPackStore.AddEntry(
            pack,
            LocalCoachPackState.Enabled,
            LocalCoachPackOrigin.Imported);
        var library = OpenLibrary(context);
        context.Coaching.Library.RefreshAsync().GetAwaiter().GetResult();
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
        OpenPack(library, pack.Id);

        Assert.True(context.Coaching.Details.IsImported);
        Assert.Contains("оригинал автора", context.Coaching.Details.DeleteConfirmationMessage);
        Assert.Equal(
            "Создать свою копию",
            WpfTestHost.Find<Button>(
                context.CoachingPackDetailsPage,
                "PackEditActionButton").Content);
        var actionsPopup = OpenActionsPopup(context.CoachingPackDetailsPage);
        Assert.Equal(
            "Удалить установку",
            FindPopupButton(actionsPopup, "DeleteMenuButton").Content);
        actionsPopup.IsOpen = false;
    });

    private static Popup OpenActionsPopup(FrameworkElement detailsPage)
    {
        var menu = WpfTestHost.Find<CoachPackActionsMenu>(detailsPage, "PackActionsMenu");
        var actionsButton = WpfTestHost.Find<Button>(menu, "AdditionalActionsButton");
        WpfTestHost.Click(actionsButton);
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
        var popup = WpfTestHost.Find<Popup>(menu, "ActionsPopup");
        Assert.True(popup.IsOpen);
        return popup;
    }

    private static Button FindPopupButton(Popup popup, string name) =>
        WpfTestHost.Find<Button>(
            Assert.IsAssignableFrom<FrameworkElement>(popup.Child),
            name);

    private static void OpenPack(FrameworkElement library, string packId)
    {
        var openButton = Assert.Single(
            WpfTestHost.FindVisualDescendants<Button>(library),
            button => button.Name == "OpenCoachingPackButton"
                      && button.DataContext is CoachingPackCardViewModel card
                      && card.Pack.Id == packId);
        WpfTestHost.Click(openButton);
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
    }
}

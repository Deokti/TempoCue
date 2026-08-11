using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TempoCue.App.ViewModels;
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
        Assert.Equal("Редактировать", editButton.Content);
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
        Assert.Equal("Создать свою копию", editButton.Content);
        WpfTestHost.Click(editButton);
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        Assert.Equal(Visibility.Visible, context.MatchupEditorPage.Visibility);
        Assert.Equal(MatchupEditorMode.Copy, context.Coaching.MatchupEditor.Mode);
    });

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

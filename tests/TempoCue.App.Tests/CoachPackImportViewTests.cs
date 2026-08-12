using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TempoCue.App.ViewModels;
using TempoCue.Core;
using static TempoCue.App.Tests.CoachingViewTestContext;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class CoachPackImportViewTests
{
    [Fact]
    public void ValidPack_ShowsPreviewAndCanBeImportedFromDialog() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        OpenLibrary(context);
        var pack = TestCoachPackFactory.CreateDetailedMatchup() with
        {
            Id = "matchup.import-view.new",
            Name = "Импорт через модальное окно",
            Description = "На момент написания гайда этот соперник остаётся сложным матчапом. "
                + "Описание намеренно занимает несколько строк, чтобы проверить расстояние "
                + "между карточкой предпросмотра и нижними кнопками модального окна."
        };
        using var source = new MemoryStream();
        CoachPackWriter.WriteAsync(source, pack).GetAwaiter().GetResult();
        source.Position = 0;

        context.Coaching.OpenImportAsync(source).GetAwaiter().GetResult();
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        var dialog = context.CoachPackImportDialog;
        Assert.Equal(Visibility.Visible, dialog.Visibility);
        Assert.Equal(pack.Name, context.Coaching.Import.PackName);
        var confirmButton = WpfTestHost.Find<Button>(dialog, "ConfirmImportButton");
        var previewCard = WpfTestHost.Find<Border>(dialog, "ImportPreviewCard");
        Assert.True(confirmButton.IsVisible);
        Assert.True(confirmButton.IsEnabled);
        Assert.Equal("Импортировать", confirmButton.Content);
        var previewBottom = previewCard
            .TransformToAncestor(dialog)
            .Transform(new Point(0, previewCard.ActualHeight))
            .Y;
        var buttonTop = confirmButton
            .TransformToAncestor(dialog)
            .Transform(new Point())
            .Y;
        Assert.True(buttonTop - previewBottom >= 15);
        SaveVisualSnapshotIfRequested(context.Window, "coach-pack-import-preview.png");

        WpfTestHost.Click(confirmButton);
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        Assert.False(context.Coaching.Import.IsOpen);
        var card = context.Coaching.Library.FindCard(pack.Id);
        Assert.NotNull(card);
        Assert.Equal(CoachingPackOrigin.Imported, card.Origin);
        Assert.False(card.IsEnabled);
    });

    [Fact]
    public void DamagedFile_ShowsErrorWithoutImportAction() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        OpenLibrary(context);
        using var source = new MemoryStream("{ damaged"u8.ToArray());

        context.Coaching.OpenImportAsync(source).GetAwaiter().GetResult();
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        var dialog = context.CoachPackImportDialog;
        Assert.True(context.Coaching.Import.HasError);
        Assert.False(WpfTestHost.Find<Button>(dialog, "ConfirmImportButton").IsVisible);
        SaveVisualSnapshotIfRequested(context.Window, "coach-pack-import-invalid.png");
    });

    [Fact]
    public void ValidMapControlPack_ShowsPreviewAndCanBeImportedFromDialog() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        OpenLibrary(context);
        var pack = TestMapControlPackFactory.Create("map-control.import-view.new") with
        {
            Name = "Контроль нейтральных объектов",
            Description = "Напоминания о нейтральных объектах и подготовке к ним."
        };
        using var source = new MemoryStream();
        MapControlPackWriter.WriteAsync(source, pack).GetAwaiter().GetResult();
        source.Position = 0;

        context.Coaching.OpenImportAsync(source).GetAwaiter().GetResult();
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        var dialog = context.CoachPackImportDialog;
        Assert.Equal(Visibility.Visible, dialog.Visibility);
        Assert.True(context.Coaching.Import.IsMapControlPreview);
        Assert.Contains("Контроль карты", context.Coaching.Import.PreviewContextLabel);
        var confirmButton = WpfTestHost.Find<Button>(dialog, "ConfirmImportButton");
        Assert.True(confirmButton.IsVisible);
        Assert.True(confirmButton.IsEnabled);
        SaveVisualSnapshotIfRequested(context.Window, "map-control-pack-import-preview.png");

        WpfTestHost.Click(confirmButton);
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        Assert.False(context.Coaching.Import.IsOpen);
        var card = context.Coaching.Library.FindMapControlCard(pack.Id);
        Assert.NotNull(card);
        Assert.Equal(CoachingPackOrigin.Imported, card.Origin);
        Assert.False(card.IsEnabled);
    });
}

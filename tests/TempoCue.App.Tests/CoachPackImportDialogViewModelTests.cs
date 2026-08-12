using System.IO;
using System.Text;
using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class CoachPackImportDialogViewModelTests
{
    [Fact]
    public async Task OpenAndConfirm_ImportsPackAndRefreshesLibrary()
    {
        var store = new TestCoachPackStore();
        using var coaching = CoachingViewModelTestFactory.Create(
            TestCoachPackFactory.CreateMatchup(
                id: "matchup.built-in.import-test",
                name: "Встроенный набор"),
            LocalizationService.Instance,
            store);
        var pack = TestCoachPackFactory.CreateDetailedMatchup() with
        {
            Id = "matchup.import-dialog.new",
            Name = "Импортируемый матчап"
        };
        await using var source = await CreatePortableStreamAsync(pack);

        await coaching.OpenImportAsync(source);

        Assert.True(coaching.Import.IsOpen);
        Assert.True(coaching.Import.HasPreview);
        Assert.True(coaching.Import.CanConfirm);
        Assert.Equal(pack.Name, coaching.Import.PackName);
        Assert.Contains("Urgot", coaching.Import.MatchupLabel);
        Assert.Contains("Sett", coaching.Import.MatchupLabel);

        var imported = await coaching.ConfirmImportAsync();

        Assert.True(imported);
        Assert.False(coaching.Import.IsOpen);
        var card = coaching.Library.FindCard(pack.Id);
        Assert.NotNull(card);
        Assert.Equal(CoachingPackOrigin.Imported, card.Origin);
        Assert.False(card.IsEnabled);
        Assert.True(coaching.Library.IsLibraryMessageSuccess);
        Assert.Contains(pack.Name, coaching.Library.LibraryMessage);
    }

    [Fact]
    public async Task OpenAndConfirm_MapControlFile_ImportsPackAndRefreshesMapControlLibrary()
    {
        var mapControlStore = new TestMapControlPackStore();
        using var coaching = CoachingViewModelTestFactory.Create(
            TestCoachPackFactory.CreateMatchup(),
            LocalizationService.Instance,
            mapControlStore: mapControlStore);
        var pack = TestMapControlPackFactory.Create("map-control.import-dialog.new") with
        {
            Name = "Импортируемый контроль карты"
        };
        await using var source = await CreatePortableStreamAsync(pack);

        await coaching.OpenImportAsync(source);

        Assert.True(coaching.Import.IsOpen);
        Assert.True(coaching.Import.HasPreview);
        Assert.True(coaching.Import.IsMapControlPreview);
        Assert.True(coaching.Import.CanConfirm);
        Assert.Equal(pack.Name, coaching.Import.PackName);
        Assert.Contains("Контроль карты", coaching.Import.PreviewContextLabel);

        var imported = await coaching.ConfirmImportAsync();

        Assert.True(imported);
        Assert.False(coaching.Import.IsOpen);
        var card = coaching.Library.FindMapControlCard(pack.Id);
        Assert.NotNull(card);
        Assert.Equal(CoachingPackOrigin.Imported, card.Origin);
        Assert.False(card.IsEnabled);
        Assert.Equal(pack.Id, mapControlStore.SavedPack?.Id);
        Assert.Equal(pack.Name, mapControlStore.SavedPack?.Name);
    }

    [Fact]
    public async Task OpenAsync_DamagedJson_ShowsLocalizedBlockingStatus()
    {
        var localization = LocalizationService.Instance;
        var store = new TestCoachPackStore();
        using var dialog = new CoachPackImportDialogViewModel(
            localization,
            new ImportPortableCoachPackUseCase(
                new ImportCoachPackUseCase(store, []),
                new ImportMapControlPackUseCase(new TestMapControlPackStore(), [])));
        await using var source = new MemoryStream(Encoding.UTF8.GetBytes("{ damaged"));

        await dialog.OpenAsync(source);

        Assert.True(dialog.IsOpen);
        Assert.True(dialog.HasError);
        Assert.False(dialog.HasPreview);
        Assert.False(dialog.HasImportAction);
        Assert.False(dialog.CanConfirm);
        Assert.Equal(
            localization.Get("Coaching.Import.Status.Invalid.Title"),
            dialog.StatusTitle);
    }

    private static async Task<MemoryStream> CreatePortableStreamAsync(CoachPack pack)
    {
        var stream = new MemoryStream();
        await CoachPackWriter.WriteAsync(stream, pack);
        stream.Position = 0;
        return stream;
    }

    private static async Task<MemoryStream> CreatePortableStreamAsync(MapControlPack pack)
    {
        var stream = new MemoryStream();
        await MapControlPackWriter.WriteAsync(stream, pack);
        stream.Position = 0;
        return stream;
    }
}

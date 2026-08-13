using System.IO;
using System.Text;
using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class MapControlPackDetailsViewModelTests
{
    [Fact]
    public async Task ExportAsync_WritesSelectedPackAsPortableJson()
    {
        var store = new TestMapControlPackStore();
        var pack = TestMapControlPackFactory.Create("map-control.export-test");
        await store.SaveAsync(pack, LocalCoachPackState.Enabled);
        using var coaching = CoachingViewModelTestFactory.Create(
            TestCoachPackFactory.CreateMatchup(),
            LocalizationService.Instance,
            mapControlStore: store);
        await coaching.Library.EnsureLoadedAsync();
        var card = coaching.Library.FindMapControlCard(pack.Id);
        Assert.NotNull(card);
        coaching.OpenMapControlPackDetails(card);
        await using var destination = new MemoryStream();

        var exported = await coaching.MapControlDetails.ExportAsync(destination);

        Assert.True(exported);
        Assert.True(coaching.MapControlDetails.HasExportMessage);
        var json = Encoding.UTF8.GetString(destination.ToArray());
        Assert.Contains(pack.Id, json, StringComparison.Ordinal);
        Assert.Contains("\"schemaVersion\"", json, StringComparison.Ordinal);
    }
}

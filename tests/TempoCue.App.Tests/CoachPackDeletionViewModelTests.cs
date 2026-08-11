using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class CoachPackDeletionViewModelTests
{
    [Fact]
    public async Task DeleteSelectedPackAsync_RemovesLocalPackAndReturnsToLibrary()
    {
        var store = new TestCoachPackStore();
        var localPack = TestCoachPackFactory.CreateDetailedMatchup() with
        {
            Id = "matchup.local.delete-test",
            Name = "Локальный матчап"
        };
        await store.SaveAsync(localPack, LocalCoachPackState.Enabled);
        using var viewModel = CoachingViewModelTestFactory.Create(
            TestCoachPackFactory.CreateMatchup(
                id: "matchup.built-in.delete-test",
                name: "Встроенный матчап"),
            LocalizationService.Instance,
            store);
        await viewModel.Library.EnsureLoadedAsync();
        var localCard = viewModel.Library.FindCard(localPack.Id);
        Assert.NotNull(localCard);
        await viewModel.OpenPackDetailsAsync(localCard);

        var deleted = await viewModel.DeleteSelectedPackAsync();

        Assert.True(deleted);
        Assert.Equal(CoachingScreen.Library, viewModel.CurrentScreen);
        Assert.Null(viewModel.Library.FindCard(localPack.Id));
        Assert.True(viewModel.Library.IsLibraryMessageSuccess);
        Assert.Contains(localCard.Title, viewModel.Library.LibraryMessage);
        Assert.Empty((await store.LoadAsync()).Entries);
    }

    [Fact]
    public async Task DeleteSelectedPackAsync_DoesNotDeleteBuiltInPack()
    {
        var builtInPack = TestCoachPackFactory.CreateDetailedMatchup() with
        {
            Id = "matchup.built-in.protected",
            Name = "Встроенный матчап"
        };
        using var viewModel = CoachingViewModelTestFactory.Create(
            builtInPack,
            LocalizationService.Instance);
        await viewModel.Library.EnsureLoadedAsync();
        var builtInCard = viewModel.Library.FindCard(builtInPack.Id);
        Assert.NotNull(builtInCard);
        await viewModel.OpenPackDetailsAsync(builtInCard);

        var deleted = await viewModel.DeleteSelectedPackAsync();

        Assert.False(deleted);
        Assert.False(viewModel.Details.CanDelete);
        viewModel.OpenDeleteConfirmation();
        Assert.False(viewModel.IsDeleteConfirmationOpen);
        Assert.Equal(CoachingScreen.PackDetails, viewModel.CurrentScreen);
        Assert.NotNull(viewModel.Library.FindCard(builtInPack.Id));
    }
}

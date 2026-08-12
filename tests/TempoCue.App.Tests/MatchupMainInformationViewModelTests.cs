using TempoCue.App.Localization;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Tests;

/// <summary>Проверяет первый шаг редактора матчапа.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MatchupMainInformationViewModelTests
{
    [Fact]
    public async Task MainInformation_RequiresOnlyFirstStepMetadata()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("ru-RU");

        try
        {
            var viewModel = new MatchupMainInformationViewModel(
                "ru-RU",
                TestGamePatchCatalog.Ready(),
                localization);

            Assert.Equal("ru-RU", viewModel.AdviceLanguageCode);
            Assert.False(viewModel.IsComplete);
            Assert.Equal("Новый матчап", viewModel.DisplayTitle);
            Assert.Equal("От 3 до 80 символов.", viewModel.PackNameLengthFeedback.Text);
            Assert.False(viewModel.PackNameLengthFeedback.HasError);

            viewModel.ShortDescription = "3213123";

            Assert.Equal(
                "7 из минимум 10 символов. Максимум: 240.",
                viewModel.ShortDescriptionLengthFeedback.Text);
            Assert.True(viewModel.ShortDescriptionLengthFeedback.HasError);
            Assert.False(viewModel.IsComplete);

            viewModel.PackName = "Urgot против Sett";
            viewModel.ShortDescription = "Как пережить раннюю линию и найти момент для боя.";
            viewModel.Author = "TopLaneAuthor";
            await viewModel.EnsureGamePatchCatalogLoadedAsync();

            Assert.True(viewModel.IsComplete);
            Assert.Equal("Urgot против Sett", viewModel.DisplayTitle);
            Assert.Equal("26.15", viewModel.GamePatch);
            Assert.Equal("26.15 — текущий", viewModel.SelectedGamePatch?.DisplayName);
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }
}

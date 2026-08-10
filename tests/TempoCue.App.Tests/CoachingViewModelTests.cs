using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

/// <summary>Проверяет подготовку Coach Pack для библиотеки коучинга.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class CoachingViewModelTests
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

    [Fact]
    public async Task CoachingNavigation_OpensOnlyAvailablePackEditor()
    {
        var localization = LocalizationService.Instance;
        using var viewModel = new CoachingViewModel(
            new CoachPack
            {
                SchemaVersion = 1,
                Id = "test.navigation",
                Name = "Test",
                Patch = "test",
                Rules = []
            },
            localization,
            TestGamePatchCatalog.Ready(),
            TestChampionCatalog.Ready(),
            TestItemCatalog.Ready());

        Assert.Equal(CoachingScreen.Library, viewModel.CurrentScreen);
        Assert.True(viewModel.PackTypeSelection.IsMatchupAvailable);
        Assert.False(viewModel.PackTypeSelection.IsGameControlAvailable);

        viewModel.OpenPackTypeSelection();
        Assert.Equal(CoachingScreen.PackTypeSelection, viewModel.CurrentScreen);

        viewModel.OpenMatchupEditor();
        Assert.Equal(CoachingScreen.MatchupEditor, viewModel.CurrentScreen);
        Assert.Equal(1, viewModel.MatchupEditor.CurrentStep);
        Assert.Equal(5, viewModel.MatchupEditor.TotalSteps);
        Assert.False(viewModel.MatchupEditor.MoveNext());

        var mainInformation = viewModel.MatchupEditor.MainInformation;
        mainInformation.PackName = "Urgot против Sett";
        mainInformation.ShortDescription = "Как пережить раннюю линию и найти момент для боя.";
        mainInformation.Author = "TopLaneAuthor";
        await mainInformation.EnsureGamePatchCatalogLoadedAsync();

        Assert.True(viewModel.MatchupEditor.MoveNext());
        Assert.Equal(2, viewModel.MatchupEditor.CurrentStep);
        Assert.False(viewModel.MatchupEditor.CanMoveNext);

        var championsAndRole = viewModel.MatchupEditor.ChampionsAndRole;
        await championsAndRole.EnsureCatalogLoadedAsync();
        championsAndRole.SelectedPlayerChampion = championsAndRole.Champions
            .Single(champion => champion.Id == "Urgot");
        championsAndRole.SelectedOpponentChampion = championsAndRole.Champions
            .Single(champion => champion.Id == "Sett");

        Assert.True(viewModel.MatchupEditor.CanMoveNext);
        Assert.True(viewModel.MatchupEditor.MoveNext());
        Assert.Equal(3, viewModel.MatchupEditor.CurrentStep);
        Assert.False(viewModel.MatchupEditor.CanMoveNext);

        var content = viewModel.MatchupEditor.Content;
        content.BeginAddPhase();
        content.Editor.Name = "Осторожное начало";
        content.Editor.PrimaryAdvice = "Не ищи размен первым";
        Assert.True(content.SaveEditor());
        Assert.Single(content.Phases);

        Assert.True(viewModel.MatchupEditor.MovePrevious());
        Assert.Equal(2, viewModel.MatchupEditor.CurrentStep);
        Assert.True(viewModel.MatchupEditor.MoveNext());
        Assert.Equal(3, viewModel.MatchupEditor.CurrentStep);
        Assert.Single(content.Phases);

        viewModel.OpenLibrary();
        Assert.Equal(CoachingScreen.Library, viewModel.CurrentScreen);
    }

    [Fact]
    public void MatchupContent_ManagesPhaseDraftWithoutMutatingUnsavedChanges()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("ru-RU");

        try
        {
            var content = new MatchupContentViewModel(localization, TestItemCatalog.Ready());

            content.BeginAddPhase();
            Assert.True(content.Editor.IsOpen);
            Assert.False(content.Editor.CanSave);
            content.Editor.Name = "Осторожное начало";
            content.Editor.PrimaryAdvice = "Не ищи размен первым";
            content.Editor.WhyNow = "Sett сильнее на первых уровнях";

            Assert.True(content.Editor.CanSave);
            Assert.True(content.SaveEditor());
            var original = Assert.Single(content.Phases);
            Assert.Equal("phase-1", original.Id);
            Assert.Equal(1, original.Order);
            Assert.False(original.CanMoveUp);
            Assert.False(original.CanMoveDown);

            content.BeginEditPhase(original);
            content.Editor.Name = "Несохранённое название";
            content.CancelEditor();
            Assert.Equal("Осторожное начало", original.Name);

            var duplicate = content.DuplicatePhase(original);
            Assert.Equal(2, content.Phases.Count);
            Assert.Equal("Осторожное начало — копия", duplicate.Name);
            Assert.True(original.CanMoveDown);
            Assert.True(duplicate.CanMoveUp);

            Assert.True(content.MovePhaseUp(duplicate));
            Assert.Same(duplicate, content.Phases[0]);
            Assert.Equal(1, duplicate.Order);
            Assert.Equal(2, original.Order);

            content.BeginEditPhase(original);
            content.Editor.Name = "Обновлённая фаза";
            Assert.True(content.SaveEditor());
            Assert.Equal("Обновлённая фаза", original.Name);

            Assert.True(content.DeletePhase(duplicate));
            Assert.Single(content.Phases);
            Assert.Equal(1, original.Order);
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }

    [Fact]
    public void MatchupContent_EditorActionsDuplicateAndDeleteCurrentPhase()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("en-US");

        try
        {
            var content = new MatchupContentViewModel(localization, TestItemCatalog.Ready());
            content.BeginAddPhase();
            Assert.Equal("Phase is active at levels 1+", content.Editor.MeaningSummary);
            content.Editor.ExtendsToEnd = false;
            Assert.Equal("Phase is active at levels 1–18", content.Editor.MeaningSummary);
            content.Editor.ExtendsToEnd = true;
            content.Editor.Name = "Early lane";
            content.Editor.PrimaryAdvice = "Avoid extended trades";

            var firstCopy = Assert.IsType<MatchupPhaseViewModel>(content.DuplicateEditor());
            Assert.Equal(2, content.Phases.Count);
            Assert.Equal("phase-2", firstCopy.Id);
            Assert.Equal(firstCopy.Id, content.Editor.EditingPhaseId);
            Assert.Equal("Early lane — copy", firstCopy.Name);

            Assert.True(content.DeleteEditor());
            Assert.False(content.Editor.IsOpen);
            var original = Assert.Single(content.Phases);
            Assert.Equal("Early lane", original.Name);

            content.BeginEditPhase(original);
            content.Editor.Name = "Edited only for copy";
            var editedCopy = Assert.IsType<MatchupPhaseViewModel>(content.DuplicateEditor());

            Assert.Equal("Early lane", original.Name);
            Assert.Equal("Edited only for copy — copy", editedCopy.Name);
            Assert.Equal(editedCopy.Id, content.Editor.EditingPhaseId);
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }

    [Fact]
    public async Task MatchupContent_ItemPickerCreatesStartAndEndConditions()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("ru-RU");

        try
        {
            var content = new MatchupContentViewModel(localization, TestItemCatalog.Ready());
            content.SetItemCatalogContext("26.15", "ru-RU");
            await content.EnsureItemCatalogLoadedAsync();
            content.BeginAddPhase();
            content.Editor.Name = "Пик силы";
            content.Editor.PrimaryAdvice = "Можно принимать контролируемый бой";

            content.OpenStartItemPicker();
            Assert.Equal(ItemPickerTarget.PhaseStart, content.ItemPicker.Target);
            var cleaver = content.ItemPicker.Items.Single(item => item.Id == 3071);
            content.ItemPicker.Select(cleaver);

            Assert.Equal(3071, content.Editor.StartItem?.Id);
            Assert.Contains("после покупки «Чёрная секира»", content.Editor.MeaningSummary);

            content.OpenEndItemPicker();
            content.ItemPicker.Select(cleaver);

            Assert.True(content.Editor.HasConflictingItemConditions);
            Assert.False(content.Editor.CanSave);

            content.OpenEndItemPicker();
            var boots = content.ItemPicker.Items.Single(item => item.Id == 3047);
            content.ItemPicker.Select(boots);

            Assert.False(content.Editor.HasConflictingItemConditions);
            Assert.True(content.SaveEditor());
            var phase = Assert.Single(content.Phases);
            Assert.Equal(3071, phase.StartItem?.Id);
            Assert.Equal(3047, phase.EndItem?.Id);
            Assert.Equal(
                "Начинается после: Чёрная секира · Заканчивается после: Бронированные сапоги",
                phase.ItemConditionSummary);

            content.BeginEditPhase(phase);
            Assert.Equal(3071, content.Editor.StartItem?.Id);
            Assert.Equal(3047, content.Editor.EndItem?.Id);
            content.RemoveEndItem();
            Assert.False(content.Editor.HasEndItem);
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }

    [Fact]
    public async Task ChampionsAndRole_SelectsCatalogEntriesAndRejectsMirrorMatchup()
    {
        var viewModel = new MatchupChampionsAndRoleViewModel(
            TestChampionCatalog.Ready(),
            LocalizationService.Instance);

        Assert.Equal(MatchupLaneRole.Top, viewModel.SelectedRole);
        Assert.False(viewModel.IsComplete);
        Assert.Equal(CatalogViewState.NotLoaded, viewModel.CatalogState);

        await viewModel.EnsureCatalogLoadedAsync();
        Assert.Equal(CatalogViewState.Ready, viewModel.CatalogState);

        var urgot = Assert.Single(viewModel.Champions, champion => champion.Id == "Urgot");
        Assert.Equal("Urgot", urgot.DisplayName);

        var sett = Assert.Single(viewModel.Champions, champion => champion.Id == "Sett");
        Assert.Equal("Sett", sett.DisplayName);

        viewModel.SelectedPlayerChampion = urgot;
        viewModel.SelectedOpponentChampion = urgot;
        Assert.True(viewModel.HasSameChampion);
        Assert.False(viewModel.IsComplete);

        viewModel.SelectedOpponentChampion = sett;
        Assert.False(viewModel.HasSameChampion);
        Assert.True(viewModel.IsComplete);
    }

    [Fact]
    public async Task ChampionsAndRole_DistinguishesCachedCatalogAndLoadFailure()
    {
        var cachedCatalog = new TestChampionCatalog(
            new ChampionCatalogLoadResult(
                [
                    new ChampionCatalogEntry(
                        "Urgot",
                        "Urgot",
                        new Uri("https://example.test/Urgot.png"))
                ],
                ChampionCatalogSource.Cache));
        var cachedViewModel = new MatchupChampionsAndRoleViewModel(
            cachedCatalog,
            LocalizationService.Instance);

        await cachedViewModel.EnsureCatalogLoadedAsync();

        Assert.Equal(CatalogViewState.Cached, cachedViewModel.CatalogState);
        Assert.True(cachedViewModel.IsCatalogReady);
        Assert.True(cachedViewModel.IsUsingCache);

        var unavailableViewModel = new MatchupChampionsAndRoleViewModel(
            new TestChampionCatalog(ChampionCatalogLoadResult.Unavailable()),
            LocalizationService.Instance);

        await unavailableViewModel.EnsureCatalogLoadedAsync();

        Assert.Equal(CatalogViewState.Failed, unavailableViewModel.CatalogState);
        Assert.True(unavailableViewModel.HasLoadError);
        Assert.False(unavailableViewModel.IsCatalogReady);
    }

    [Fact]
    public async Task ChampionsAndRole_PickerSearchesAndUpdatesRequestedSide()
    {
        var viewModel = new MatchupChampionsAndRoleViewModel(
            TestChampionCatalog.Ready(),
            LocalizationService.Instance);
        await viewModel.EnsureCatalogLoadedAsync();

        viewModel.OpenPlayerPicker();

        Assert.True(viewModel.IsPickerOpen);
        Assert.Equal(ChampionPickerTarget.Player, viewModel.PickerTarget);
        viewModel.PickerSearch = "set";
        var sett = Assert.Single(viewModel.FilteredPickerChampions);

        viewModel.SelectPickerChampion(sett);

        Assert.False(viewModel.IsPickerOpen);
        Assert.Equal("Sett", viewModel.SelectedPlayerChampion?.Id);

        viewModel.OpenOpponentPicker();
        var urgot = viewModel.FilteredPickerChampions.Single(champion => champion.Id == "Urgot");
        viewModel.SelectPickerChampion(urgot);

        Assert.Equal("Urgot", viewModel.SelectedOpponentChampion?.Id);
        Assert.True(viewModel.IsComplete);
    }

    [Fact]
    public void LanguageChange_UpdatesSystemLabelsButPreservesPackContent()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("ru-RU");

        try
        {
            var pack = new CoachPack
            {
                SchemaVersion = 1,
                Id = "test.urgot-vs-sett",
                Name = "Urgot vs Sett — авторский текст",
                Patch = "требует проверки",
                Rules =
                [
                    CreateRule(
                        "early",
                        1,
                        8,
                        PowerState.Dangerous,
                        "Не принимай ранний бой"),
                    CreateRule(
                        "late",
                        9,
                        null,
                        PowerState.Playable,
                        "Можно играть активнее")
                ]
            };
            using var viewModel = new CoachingViewModel(
                pack,
                localization,
                TestGamePatchCatalog.Ready(),
                TestChampionCatalog.Ready(),
                TestItemCatalog.Ready());

            Assert.Equal("URG", viewModel.PlayerChampionInitials);
            Assert.Equal("SET", viewModel.OpponentChampionInitials);
            Assert.Equal("2 правил", viewModel.RulesSummary);
            Assert.Equal("Опасно", viewModel.Rules[0].PowerStateLabel);
            Assert.Equal("ур. 1–8", viewModel.Rules[0].LevelRange);
            Assert.Equal("ур. 9+", viewModel.Rules[1].LevelRange);

            localization.SetLanguage("en-US");

            Assert.Equal("2 rules", viewModel.RulesSummary);
            Assert.Equal("Danger", viewModel.Rules[0].PowerStateLabel);
            Assert.Equal("lvl. 1–8", viewModel.Rules[0].LevelRange);
            Assert.Equal("Не принимай ранний бой", viewModel.Rules[0].Title);
            Assert.Equal("Urgot vs Sett — авторский текст", viewModel.PackName);
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }

    private static CoachRuleDefinition CreateRule(
        string id,
        int minimumLevel,
        int? maximumLevel,
        PowerState powerState,
        string title) => new()
        {
            Id = id,
            PlayerChampion = "Urgot",
            OpponentChampion = "Sett",
            MinimumLevel = minimumLevel,
            MaximumLevel = maximumLevel,
            PowerState = powerState,
            Priority = 1,
            Title = title,
            Message = "Содержимое набора остаётся на языке автора.",
            Source = "Тестовый источник"
        };
}

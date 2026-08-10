using TempoCue.App.Localization;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Tests;

/// <summary>Проверяет управление фазами и их предметными рекомендациями.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MatchupContentViewModelTests
{
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
            Assert.Equal("От 3 до 80 символов.", content.Editor.NameLengthFeedback.Text);
            Assert.Equal("От 3 до 200 символов.", content.Editor.PrimaryAdviceLengthFeedback.Text);
            content.Editor.PrimaryAdvice = "Не";
            Assert.True(content.Editor.PrimaryAdviceLengthFeedback.HasError);
            Assert.Equal(
                "2 из минимум 3 символов. Максимум: 200.",
                content.Editor.PrimaryAdviceLengthFeedback.Text);
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
    public async Task MatchupContent_ItemPickerCreatesRecommendedItemTargets()
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

            content.OpenRecommendedItemPicker();
            Assert.Equal(ItemPickerTarget.PhaseRecommendations, content.ItemPicker.Target);
            var cleaver = content.ItemPicker.Items.Single(item => item.Id == 3071);
            var boots = content.ItemPicker.Items.Single(item => item.Id == 3047);
            content.ItemPicker.ToggleSelection(cleaver);
            Assert.Equal(3000, content.ItemPicker.PreviewedItem?.Price);
            content.ItemPicker.ToggleSelection(boots);
            Assert.Equal(2, content.ItemPicker.SelectedItems.Count);
            Assert.True(content.ItemPicker.ConfirmSelection());

            Assert.Equal([3071, 3047], content.Editor.RecommendedItems.Select(item => item.Id));
            Assert.Contains(
                "Ориентир по предметам: Чёрная секира, Бронированные сапоги",
                content.Editor.MeaningSummary);

            content.OpenRecommendedItemPicker();
            Assert.All(content.ItemPicker.SelectedItems, item => Assert.True(item.IsSelected));
            content.ItemPicker.ToggleSelection(boots);
            Assert.True(content.ItemPicker.ConfirmSelection());

            Assert.True(content.SaveEditor());
            var phase = Assert.Single(content.Phases);
            Assert.Equal(3071, Assert.Single(phase.RecommendedItems).Id);
            Assert.Equal(
                "Рекомендуемые предметы: Чёрная секира",
                phase.RecommendedItemsSummary);

            content.BeginEditPhase(phase);
            Assert.Equal(3071, Assert.Single(content.Editor.RecommendedItems).Id);
            content.OpenRecommendedItemPicker();
            content.ItemPicker.ToggleSelection(cleaver);
            Assert.False(content.ItemPicker.HasSelectedItems);
            content.ItemPicker.Close();
            Assert.Equal(3071, Assert.Single(content.Editor.RecommendedItems).Id);

            content.RemoveRecommendedItem(content.Editor.RecommendedItems[0]);
            Assert.False(content.Editor.HasRecommendedItems);
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }
}

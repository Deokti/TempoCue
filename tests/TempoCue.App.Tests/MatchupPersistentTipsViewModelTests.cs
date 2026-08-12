using TempoCue.App.Localization;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Tests;

/// <summary>Проверяет жизненный цикл постоянных подсказок и их связь с фазами.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MatchupPersistentTipsViewModelTests
{
    [Fact]
    public void PersistentTips_ManageIsolatedDraftAndCustomSource()
    {
        RunWithContent(content =>
        {
            content.ShowPersistentTips();
            Assert.True(content.IsPersistentTipsSection);
            Assert.Equal("Persistent tips 0", content.PersistentTipsTabLabel);

            var tips = content.PersistentTips;
            tips.BeginAdd();
            Assert.Equal("New tip", tips.Editor.Title);
            Assert.Equal("Tip 1 of 1", tips.EditorProgressLabel);
            Assert.False(tips.Editor.CanSave);

            tips.Editor.Title = "Haymaker center";
            tips.Editor.Text = "Stand near the edge of the cone to avoid true damage.";
            tips.Editor.UsesCustomSource = true;
            tips.Editor.CustomSource = "Personal matchup review";

            Assert.True(tips.Editor.CanSave);
            Assert.True(tips.SaveEditor());
            var original = Assert.Single(tips.Tips);
            Assert.Equal("Whole match", original.ScopeLabel);
            Assert.Equal("Source: Personal matchup review", original.SourceLabel);
            Assert.Equal("Persistent tips 1", content.PersistentTipsTabLabel);

            tips.BeginEdit(original);
            tips.Editor.Title = "Unsaved title";
            tips.Editor.UsesCustomSource = false;
            tips.Editor.Close();
            Assert.Equal("Haymaker center", original.Title);
            Assert.Equal("Source: Personal matchup review", original.SourceLabel);

            var copy = tips.Duplicate(original);
            Assert.Equal("Haymaker center — copy", copy.Title);
            Assert.Equal(2, tips.Count);
            Assert.True(tips.Delete(copy));
            Assert.Single(tips.Tips);
        });
    }

    [Fact]
    public void PersistentTips_PhaseScopeFallsBackToWholeMatchWhenPhaseIsDeleted()
    {
        RunWithContent(content =>
        {
            content.BeginAddPhase();
            content.Editor.Name = "Early lane";
            content.Editor.PrimaryAdvice = "Avoid extended trades";
            Assert.True(content.SaveEditor());
            var phase = Assert.Single(content.Phases);

            var tips = content.PersistentTips;
            tips.BeginAdd();
            tips.Editor.Title = "Respect Facebreaker";
            tips.Editor.Text = "Move sideways after Sett starts the pull animation.";
            tips.Editor.SelectedPhaseId = phase.Id;
            Assert.True(tips.SaveEditor());
            var tip = Assert.Single(tips.Tips);

            Assert.Equal(phase.Id, tip.ApplicablePhaseId);
            Assert.Equal("Phase 1: Early lane", tip.ScopeLabel);

            Assert.True(content.DeletePhase(phase));
            Assert.Null(tip.ApplicablePhaseId);
            Assert.Equal("Whole match", tip.ScopeLabel);
        });
    }

    [Fact]
    public void PersistentTips_EditorDuplicatesAndDeletesCurrentTip()
    {
        RunWithContent(content =>
        {
            var tips = content.PersistentTips;
            tips.BeginAdd();
            tips.Editor.Title = "Track Grit";
            tips.Editor.Text = "Check Sett's Grit before committing your ultimate.";

            var copy = Assert.IsType<MatchupPersistentTipViewModel>(tips.DuplicateEditor());
            Assert.Equal(2, tips.Count);
            Assert.Equal(copy.Id, tips.Editor.EditingTipId);
            Assert.Equal("Track Grit — copy", copy.Title);

            Assert.True(tips.DeleteEditor());
            Assert.False(tips.Editor.IsOpen);
            Assert.Equal("Track Grit", Assert.Single(tips.Tips).Title);
        });
    }

    private static void RunWithContent(Action<MatchupContentViewModel> test)
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("en-US");

        try
        {
            test(new MatchupContentViewModel(localization, TestItemCatalog.Ready()));
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }
}

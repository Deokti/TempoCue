using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Преобразует содержимое Coach Pack в локализованную модель режима чтения.</summary>
internal sealed class CoachingPackDetailsContentFactory
{
    private readonly LocalizationService _localization;

    public CoachingPackDetailsContentFactory(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public CoachingPackDetailsContent Create(
        CoachPack pack,
        IReadOnlyDictionary<int, ItemCatalogEntry> items)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(items);

        var phases = pack.Rules
            .Select(rule => new CoachingPackPhaseDetailsViewModel(
                rule.Id,
                rule.Title,
                rule.Message,
                rule.WhyNow,
                rule.NextGoal,
                rule.PowerState,
                GetPowerStateLabel(rule.PowerState),
                GetLevelRange(rule.MinimumLevel, rule.MaximumLevel),
                ResolveItems(rule.RecommendedItemIds, items)))
            .ToArray();
        var phasesById = phases.ToDictionary(static phase => phase.Id, StringComparer.Ordinal);
        var persistentTips = pack.PersistentTips
            .Select(tip => new CoachingPackPersistentTipDetailsViewModel(
                tip.Title,
                tip.Message,
                tip.ApplicableRuleId is string ruleId && phasesById.TryGetValue(ruleId, out var phase)
                    ? phase.Title
                    : _localization.Get("Coaching.MatchupEditor.Persistent.Scope.WholeMatch")))
            .ToArray();
        var itemBlocks = pack.ItemBlocks
            .Select(block => new CoachingPackItemBlockDetailsViewModel(
                block.Id,
                block.Name,
                string.IsNullOrWhiteSpace(block.WhenToChoose)
                    ? _localization.Get("Coaching.Details.Items.NoHint")
                    : block.WhenToChoose,
                _localization.Plural("Count.Item", block.ItemIds.Count),
                ResolveItems(block.ItemIds, items)))
            .ToArray();

        return new CoachingPackDetailsContent(phases, persistentTips, itemBlocks);
    }

    private IReadOnlyList<CoachingPackItemDetailsViewModel> ResolveItems(
        IEnumerable<int> itemIds,
        IReadOnlyDictionary<int, ItemCatalogEntry> items) => itemIds
        .Select(itemId => items.TryGetValue(itemId, out var item)
            ? new CoachingPackItemDetailsViewModel(item.Id, item.DisplayName, item.ImageUri)
            : new CoachingPackItemDetailsViewModel(
                itemId,
                _localization.Format("Coaching.Details.Items.Unknown", itemId),
                null))
        .ToArray();

    private string GetPowerStateLabel(PowerState powerState) => _localization.Get(powerState switch
    {
        PowerState.Dangerous => "Coaching.Power.Dangerous",
        PowerState.Cautious => "Coaching.Power.Cautious",
        PowerState.Playable => "Coaching.Power.Playable",
        PowerState.Favorable => "Coaching.Power.Favorable",
        _ => throw new ArgumentOutOfRangeException(nameof(powerState))
    });

    private string GetLevelRange(int minimumLevel, int? maximumLevel) =>
        maximumLevel is int upperBound
            ? _localization.Format("Coaching.Rule.LevelRange", minimumLevel, upperBound)
            : _localization.Format("Coaching.Rule.LevelFrom", minimumLevel);
}

using TempoCue.App.Localization;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Представление сохранённой в текущем черновике фазы.</summary>
public sealed class MatchupPhaseViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private MatchupPhaseDraft _draft;
    private int _order;
    private bool _canMoveUp;
    private bool _canMoveDown;

    public MatchupPhaseViewModel(
        MatchupPhaseDraft draft,
        LocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(localization);
        _draft = draft;
        _localization = localization;
    }

    public string Id => _draft.Id;

    public string Name => _draft.Name;

    public PowerState PowerState => _draft.PowerState;

    public int MinimumLevel => _draft.MinimumLevel;

    public int? MaximumLevel => _draft.MaximumLevel;

    public string PrimaryAdvice => _draft.PrimaryAdvice;

    public string WhyNow => _draft.WhyNow;

    public string NextGoal => _draft.NextGoal;

    public MatchupItemDraft? StartItem => _draft.StartItems.FirstOrDefault();

    public MatchupItemDraft? EndItem => _draft.EndItems.FirstOrDefault();

    public bool HasItemConditions => StartItem is not null || EndItem is not null;

    public bool HasWhyNow => !string.IsNullOrWhiteSpace(WhyNow);

    public bool HasNextGoal => !string.IsNullOrWhiteSpace(NextGoal);

    public int Order
    {
        get => _order;
        private set => SetProperty(ref _order, value);
    }

    public bool CanMoveUp
    {
        get => _canMoveUp;
        private set => SetProperty(ref _canMoveUp, value);
    }

    public bool CanMoveDown
    {
        get => _canMoveDown;
        private set => SetProperty(ref _canMoveDown, value);
    }

    public string PowerStateLabel => _localization.Get(PowerState switch
    {
        PowerState.Dangerous => "Coaching.Power.Dangerous",
        PowerState.Cautious => "Coaching.Power.Cautious",
        PowerState.Playable => "Coaching.Power.Playable",
        PowerState.Favorable => "Coaching.Power.Favorable",
        _ => throw new ArgumentOutOfRangeException()
    });

    public string LevelRange => MaximumLevel is int maximumLevel
        ? _localization.Format("Coaching.Rule.LevelRange", MinimumLevel, maximumLevel)
        : _localization.Format("Coaching.Rule.LevelFrom", MinimumLevel);

    public string ItemConditionSummary => (StartItem, EndItem) switch
    {
        ({ } startItem, { } endItem) => _localization.Format(
            "Coaching.MatchupEditor.Content.Phase.Items.Both",
            startItem.DisplayName,
            endItem.DisplayName),
        ({ } startItem, null) => _localization.Format(
            "Coaching.MatchupEditor.Content.Phase.Items.Start",
            startItem.DisplayName),
        (null, { } endItem) => _localization.Format(
            "Coaching.MatchupEditor.Content.Phase.Items.End",
            endItem.DisplayName),
        _ => string.Empty
    };

    internal MatchupPhaseDraft CreateSnapshot() => _draft;

    internal void Replace(MatchupPhaseDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _draft = draft;
        OnPropertyChanged(string.Empty);
    }

    internal void UpdatePosition(int index, int count)
    {
        Order = index + 1;
        CanMoveUp = index > 0;
        CanMoveDown = index < count - 1;
    }

    internal void UpdateLanguage()
    {
        OnPropertyChanged(nameof(PowerStateLabel));
        OnPropertyChanged(nameof(LevelRange));
        OnPropertyChanged(nameof(ItemConditionSummary));
    }
}

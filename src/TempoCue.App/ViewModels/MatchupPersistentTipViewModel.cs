using TempoCue.App.Localization;

namespace TempoCue.App.ViewModels;

/// <summary>Сохранённая постоянная подсказка текущего черновика.</summary>
public sealed class MatchupPersistentTipViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private MatchupPersistentTipDraft _draft;
    private string _scopeLabel = string.Empty;

    public MatchupPersistentTipViewModel(
        MatchupPersistentTipDraft draft,
        LocalizationService localization)
    {
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public string Id => _draft.Id;

    public string Title => _draft.Title;

    public string Text => _draft.Text;

    public string? ApplicablePhaseId => _draft.ApplicablePhaseId;

    public string? CustomSource => _draft.CustomSource;

    public string ScopeLabel
    {
        get => _scopeLabel;
        private set => SetProperty(ref _scopeLabel, value);
    }

    public string SourceLabel => CustomSource is null
        ? _localization.Get("Coaching.MatchupEditor.Persistent.Source.Inherited")
        : _localization.Format("Coaching.MatchupEditor.Persistent.Source.Custom", CustomSource);

    internal MatchupPersistentTipDraft CreateSnapshot() => _draft;

    internal void Replace(MatchupPersistentTipDraft draft)
    {
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        OnPropertyChanged(string.Empty);
    }

    internal void UpdateScope(IReadOnlyList<MatchupPhaseViewModel> phases)
    {
        var phase = phases.FirstOrDefault(item => item.Id == ApplicablePhaseId);
        if (ApplicablePhaseId is not null && phase is null)
        {
            _draft = _draft with { ApplicablePhaseId = null };
        }

        ScopeLabel = phase is null
            ? _localization.Get("Coaching.MatchupEditor.Persistent.Scope.WholeMatch")
            : _localization.Format(
                "Coaching.MatchupEditor.Persistent.Scope.Phase",
                phase.Order,
                phase.Name);
        OnPropertyChanged(nameof(SourceLabel));
    }
}

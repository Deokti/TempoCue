using System.Collections.ObjectModel;
using TempoCue.App.Localization;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

public enum MatchupPhaseValidationState
{
    Empty,
    Valid,
    Warning,
    Error
}

/// <summary>Локализованное представление результата доменной проверки фаз.</summary>
public sealed class MatchupPhaseValidationViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly MatchupPhaseSetValidator _validator;
    private readonly ObservableCollection<MatchupPhaseValidationIssueViewModel> _issues = [];
    private readonly ObservableCollection<MatchupLevelCoverageViewModel> _levelCoverage = [];
    private IReadOnlyList<MatchupPhaseDraft> _phases = [];
    private MatchupPhaseValidationResult _result = new([], []);

    public MatchupPhaseValidationViewModel(
        LocalizationService localization,
        MatchupPhaseSetValidator validator)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        Issues = new ReadOnlyObservableCollection<MatchupPhaseValidationIssueViewModel>(_issues);
        LevelCoverage = new ReadOnlyObservableCollection<MatchupLevelCoverageViewModel>(_levelCoverage);
    }

    public ReadOnlyObservableCollection<MatchupPhaseValidationIssueViewModel> Issues { get; }

    public ReadOnlyObservableCollection<MatchupLevelCoverageViewModel> LevelCoverage { get; }

    public bool HasPhases => _phases.Count > 0;

    public bool HasIssues => _issues.Count > 0;

    public bool HasErrors => _result.HasErrors;

    public MatchupPhaseValidationState State => !HasPhases
        ? MatchupPhaseValidationState.Empty
        : HasErrors
            ? MatchupPhaseValidationState.Error
            : _result.HasWarnings
                ? MatchupPhaseValidationState.Warning
                : MatchupPhaseValidationState.Valid;

    public string StatusTitle => _localization.Get(State switch
    {
        MatchupPhaseValidationState.Empty => "Coaching.MatchupEditor.Content.Validation.EmptyTitle",
        MatchupPhaseValidationState.Valid => "Coaching.MatchupEditor.Content.Validation.ValidTitle",
        MatchupPhaseValidationState.Warning => "Coaching.MatchupEditor.Content.Validation.WarningTitle",
        MatchupPhaseValidationState.Error => "Coaching.MatchupEditor.Content.Validation.ErrorTitle",
        _ => throw new ArgumentOutOfRangeException()
    });

    public string StatusDescription => _localization.Get(State switch
    {
        MatchupPhaseValidationState.Empty => "Coaching.MatchupEditor.Content.Validation.EmptyDescription",
        MatchupPhaseValidationState.Valid => "Coaching.MatchupEditor.Content.Validation.ValidDescription",
        MatchupPhaseValidationState.Warning => "Coaching.MatchupEditor.Content.Validation.WarningDescription",
        MatchupPhaseValidationState.Error => "Coaching.MatchupEditor.Content.Validation.ErrorDescription",
        _ => throw new ArgumentOutOfRangeException()
    });

    public void Update(IReadOnlyList<MatchupPhaseDraft> phases)
    {
        ArgumentNullException.ThrowIfNull(phases);
        _phases = phases.ToArray();
        _result = _validator.Validate(_phases.Select(static phase =>
            new MatchupPhaseCondition(
                phase.Id,
                phase.MinimumLevel,
                phase.MaximumLevel)).ToArray());
        RefreshPresentation();
    }

    public void UpdateLanguage() => RefreshPresentation();

    private void RefreshPresentation()
    {
        var phaseNames = _phases.ToDictionary(
            static phase => phase.Id,
            static phase => phase.Name,
            StringComparer.Ordinal);
        _issues.Clear();
        foreach (var issue in _result.Issues)
        {
            _issues.Add(new MatchupPhaseValidationIssueViewModel(
                CreateIssueMessage(issue, phaseNames),
                issue.Severity));
        }

        _levelCoverage.Clear();
        foreach (var coverage in _result.LevelCoverage)
        {
            _levelCoverage.Add(new MatchupLevelCoverageViewModel(
                coverage.Level,
                coverage.Status,
                CreateCoverageTooltip(coverage)));
        }

        OnPropertyChanged(nameof(HasPhases));
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(StatusDescription));
    }

    private string CreateIssueMessage(
        MatchupPhaseValidationIssue issue,
        IReadOnlyDictionary<string, string> phaseNames)
    {
        var levelRange = FormatLevelRange(issue.MinimumLevel, issue.MaximumLevel);
        return issue.Kind switch
        {
            MatchupPhaseValidationIssueKind.InvalidLevelRange => _localization.Format(
                "Coaching.MatchupEditor.Content.Validation.Issue.InvalidRange",
                FindPhaseName(issue.PhaseIds[0], phaseNames)),
            MatchupPhaseValidationIssueKind.OverlappingPhases => _localization.Format(
                "Coaching.MatchupEditor.Content.Validation.Issue.Overlap",
                FindPhaseName(issue.PhaseIds[0], phaseNames),
                FindPhaseName(issue.PhaseIds[1], phaseNames),
                levelRange),
            MatchupPhaseValidationIssueKind.UncoveredLevels => _localization.Format(
                "Coaching.MatchupEditor.Content.Validation.Issue.Gap",
                levelRange),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private string CreateCoverageTooltip(MatchupLevelCoverage coverage) => _localization.Format(
        coverage.Status switch
        {
            MatchupLevelCoverageStatus.Covered => "Coaching.MatchupEditor.Content.Validation.Coverage.Covered",
            MatchupLevelCoverageStatus.Uncovered => "Coaching.MatchupEditor.Content.Validation.Coverage.Uncovered",
            MatchupLevelCoverageStatus.Ambiguous => "Coaching.MatchupEditor.Content.Validation.Coverage.Ambiguous",
            _ => throw new ArgumentOutOfRangeException()
        },
        coverage.Level);

    private string FormatLevelRange(int? minimumLevel, int? maximumLevel) =>
        minimumLevel == maximumLevel
            ? _localization.Format(
                "Coaching.MatchupEditor.Content.Validation.LevelSingle",
                minimumLevel)
            : _localization.Format(
                "Coaching.MatchupEditor.Content.Validation.LevelRange",
                minimumLevel,
                maximumLevel);

    private static string FindPhaseName(
        string phaseId,
        IReadOnlyDictionary<string, string> phaseNames) =>
        phaseNames.TryGetValue(phaseId, out var phaseName) ? phaseName : phaseId;
}

public sealed record MatchupPhaseValidationIssueViewModel(
    string Message,
    MatchupPhaseValidationSeverity Severity);

public sealed record MatchupLevelCoverageViewModel(
    int Level,
    MatchupLevelCoverageStatus Status,
    string Tooltip);

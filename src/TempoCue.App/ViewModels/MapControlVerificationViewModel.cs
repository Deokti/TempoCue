using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Coaching;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Строит проверяемую сводку и временную шкалу черновика Map Control Pack.</summary>
public sealed class MapControlVerificationViewModel : ObservableObject
{
    private static readonly TimeSpan MinimumTimelineDuration = TimeSpan.FromMinutes(30);
    private readonly LocalizationService _localization;
    private MapControlMainInformationViewModel? _mainInformation;
    private MapControlScopeViewModel? _scope;
    private MapControlContentViewModel? _content;
    private IReadOnlyList<MapControlVerificationIssueViewModel> _issues = [];
    private IReadOnlyList<MapControlTimelineMarkerViewModel> _timelineMarkers = [];
    private int _errorCount;
    private int _warningCount;
    private string _catalogPatch = string.Empty;
    private PlayerPosition _checkPosition = PlayerPosition.Jungle;
    private MapVisionTransition _checkTransition = MapVisionTransition.Appeared;
    private bool _areRolesReported = true;
    private string _rolesSummary = string.Empty;
    private string _teamSideSummary = string.Empty;

    public MapControlVerificationViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public IReadOnlyList<MapControlVerificationIssueViewModel> Issues
    {
        get => _issues;
        private set
        {
            if (SetProperty(ref _issues, value))
            {
                OnPropertyChanged(nameof(HasIssues));
            }
        }
    }

    public IReadOnlyList<MapControlTimelineMarkerViewModel> TimelineMarkers
    {
        get => _timelineMarkers;
        private set => SetProperty(ref _timelineMarkers, value);
    }

    public int ErrorCount
    {
        get => _errorCount;
        private set
        {
            if (SetProperty(ref _errorCount, value))
            {
                NotifyStatusChanged();
            }
        }
    }

    public int WarningCount
    {
        get => _warningCount;
        private set
        {
            if (SetProperty(ref _warningCount, value))
            {
                NotifyStatusChanged();
            }
        }
    }

    public bool HasIssues => Issues.Count > 0;

    /// <summary>
    /// Показывать ли проверку наблюдения. Без правил наблюдения разыгрывать
    /// нечего, и пустой блок только занимал бы место.
    /// </summary>
    public bool HasVisionCheck => VisionRuleCount > 0;

    public IReadOnlyList<MapControlVisionPositionOptionViewModel> CheckPositionOptions =>
        _content?.VisionRules.Editor.PositionOptions ?? [];

    public IReadOnlyList<MapControlVisionTransitionOptionViewModel> CheckTransitionOptions =>
        _content?.VisionRules.Editor.TransitionOptions ?? [];

    public PlayerPosition CheckPosition
    {
        get => _checkPosition;
        set
        {
            if (SetProperty(ref _checkPosition, value))
            {
                OnPropertyChanged(nameof(CheckResult));
            }
        }
    }

    public MapVisionTransition CheckTransition
    {
        get => _checkTransition;
        set
        {
            if (SetProperty(ref _checkTransition, value))
            {
                OnPropertyChanged(nameof(CheckResult));
            }
        }
    }

    /// <summary>
    /// Сообщил ли Riot роли соперников. Выключенное состояние — не выдумка, а
    /// обычное дело: роль приходит из Live Client Data, и без неё ни одно
    /// правило наблюдения не сработает.
    /// </summary>
    public bool AreRolesReported
    {
        get => _areRolesReported;
        set
        {
            if (SetProperty(ref _areRolesReported, value))
            {
                OnPropertyChanged(nameof(CheckResult));
            }
        }
    }

    /// <summary>
    /// Что случится при разыгранном наблюдении. Сопоставление берётся из той же
    /// политики, что работает в матче: два одинаковых сопоставления в разных
    /// местах рано или поздно разойдутся, и предпросмотр начнёт врать.
    /// </summary>
    public string CheckResult
    {
        get
        {
            if (!AreRolesReported)
            {
                return _localization.Get(
                    "Coaching.MapControlEditor.Verification.Check.NoRoles");
            }

            var rule = MapAwarenessSignalPolicy.FindRule(
                _content?.VisionRules.Snapshot ?? [],
                CheckPosition,
                CheckTransition);
            return rule is null
                ? _localization.Get("Coaching.MapControlEditor.Verification.Check.NoRule")
                : rule.Message;
        }
    }

    public bool CanContinue => ErrorCount == 0;

    public bool HasErrors => ErrorCount > 0;

    public bool IsValid => ErrorCount == 0 && WarningCount == 0;

    public string StatusTitle => IsValid
        ? _localization.Get("Coaching.MapControlEditor.Verification.Status.Success")
        : _localization.Get("Coaching.MapControlEditor.Verification.Status.Attention");

    public string StatusDescription => IsValid
        ? _localization.Get("Coaching.MapControlEditor.Verification.Status.Success.Description")
        : _localization.Get("Coaching.MapControlEditor.Verification.Status.Attention.Description");

    public string ErrorCountLabel => _localization.Format(
        "Coaching.MapControlEditor.Verification.Errors",
        ErrorCount);

    public string WarningCountLabel => _localization.Format(
        "Coaching.MapControlEditor.Verification.Warnings",
        WarningCount);

    public int RuleCount => _content?.RuleCount ?? 0;

    public int VisionRuleCount => _content?.VisionRules.Count ?? 0;

    public int TimedReminderCount => _content?.TimedReminders.Count ?? 0;

    public int PersistentTipCount => _content?.PersistentTips.Count ?? 0;

    public string CatalogPatch
    {
        get => _catalogPatch;
        private set => SetProperty(ref _catalogPatch, value);
    }

    public string RolesSummary
    {
        get => _rolesSummary;
        private set => SetProperty(ref _rolesSummary, value);
    }

    public string TeamSideSummary
    {
        get => _teamSideSummary;
        private set => SetProperty(ref _teamSideSummary, value);
    }

    public string TimelineEndLabel { get; private set; } = "30:00";

    public void Update(
        MapControlMainInformationViewModel mainInformation,
        MapControlScopeViewModel scope,
        MapControlContentViewModel content)
    {
        _mainInformation = mainInformation ?? throw new ArgumentNullException(nameof(mainInformation));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _content = content ?? throw new ArgumentNullException(nameof(content));
        Rebuild();
    }

    public void UpdateLanguage()
    {
        if (_mainInformation is not null && _scope is not null && _content is not null)
        {
            Rebuild();
        }
        else
        {
            NotifyLocalizedTextChanged();
        }
    }

    private void Rebuild()
    {
        var issues = new List<MapControlVerificationIssueViewModel>();
        MapObjectiveTimingSet? timingSet = null;
        if (!MapObjectiveTimingCatalog.TryGetForPatch(_mainInformation!.GamePatch, out timingSet)
            || timingSet is null)
        {
            issues.Add(CreateIssue(
                MapControlVerificationIssueSeverity.Error,
                "Coaching.MapControlEditor.Verification.Issue.TimingsUnavailable"));
        }
        else if (timingSet.IsUsingPreviousPatchData)
        {
            issues.Add(new MapControlVerificationIssueViewModel(
                MapControlVerificationIssueSeverity.Warning,
                _localization.Format(
                    "Coaching.MapControlEditor.Verification.Issue.PreviousPatch",
                    timingSet.RequestedPatch,
                    timingSet.VerifiedThroughPatch)));
        }

        if (!_content!.HasContent)
        {
            issues.Add(CreateIssue(
                MapControlVerificationIssueSeverity.Error,
                "Coaching.MapControlEditor.Verification.Issue.EmptyContent"));
        }

        if (!_content.IsComplete)
        {
            issues.Add(CreateIssue(
                MapControlVerificationIssueSeverity.Error,
                "Coaching.MapControlEditor.Verification.Issue.OpenEditor"));
        }

        var duplicateTimes = _content.TimedReminders.Reminders
            .GroupBy(static reminder => reminder.GameTime)
            .Where(static group => group.Count() > 1)
            .Select(static group => MapControlContentText.FormatTime(group.Key))
            .ToArray();
        if (duplicateTimes.Length > 0)
        {
            issues.Add(new MapControlVerificationIssueViewModel(
                MapControlVerificationIssueSeverity.Warning,
                _localization.Format(
                    "Coaching.MapControlEditor.Verification.Issue.DuplicateTimes",
                    string.Join(", ", duplicateTimes))));
        }

        // Два правила на одну пару «роль и событие» — ошибка, а не
        // предупреждение: сработает только первое, второе останется молчаливо
        // мёртвым текстом, и заметить это в бою нельзя.
        var duplicateVisionPairs = _content.VisionRules.DuplicatePairLabels;
        if (duplicateVisionPairs.Count > 0)
        {
            issues.Add(new MapControlVerificationIssueViewModel(
                MapControlVerificationIssueSeverity.Error,
                _localization.Format(
                    "Coaching.MapControlEditor.Verification.Issue.DuplicateVisionPairs",
                    string.Join(", ", duplicateVisionPairs))));
        }

        Issues = issues;
        ErrorCount = issues.Count(static issue =>
            issue.Severity == MapControlVerificationIssueSeverity.Error);
        WarningCount = issues.Count(static issue =>
            issue.Severity == MapControlVerificationIssueSeverity.Warning);
        CatalogPatch = _mainInformation.GamePatch;
        RolesSummary = _scope!.RolesSummary;
        TeamSideSummary = _scope.TeamSideSummary;
        TimelineMarkers = BuildTimeline(timingSet);
        NotifySummaryChanged();
        NotifyLocalizedTextChanged();
    }

    private IReadOnlyList<MapControlTimelineMarkerViewModel> BuildTimeline(
        MapObjectiveTimingSet? timingSet)
    {
        var entries = new List<(TimeSpan Time, string Title, bool IsUserReminder)>();
        if (timingSet is not null)
        {
            var configuredObjectives = _content!.Rules
                .Select(static rule => rule.Rule.Objective)
                .Distinct()
                .ToHashSet();
            entries.AddRange(timingSet.Objectives
                .Where(timing => configuredObjectives.Contains(timing.Objective))
                .Select(timing => (
                    timing.InitialSpawnTime,
                    MapControlContentText.ObjectiveName(timing.Objective, _localization),
                    false)));
        }

        entries.AddRange(_content!.TimedReminders.Reminders.Select(reminder => (
            reminder.GameTime,
            reminder.Title,
            true)));
        var ordered = entries
            .OrderBy(static entry => entry.Time)
            .ThenBy(static entry => entry.IsUserReminder)
            .ThenBy(static entry => entry.Title, StringComparer.CurrentCulture)
            .ToArray();
        var maximumTime = ordered.Length == 0
            ? MinimumTimelineDuration
            : ordered[^1].Time > MinimumTimelineDuration
                ? RoundUpToFiveMinutes(ordered[^1].Time)
                : MinimumTimelineDuration;
        TimelineEndLabel = MapControlContentText.FormatTime(maximumTime);
        OnPropertyChanged(nameof(TimelineEndLabel));

        const int laneCount = 3;
        const double minimumMarkerDistance = 0.24;
        var lastPositionByLane = Enumerable.Repeat(double.NegativeInfinity, laneCount).ToArray();
        var markers = new List<MapControlTimelineMarkerViewModel>(ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            var entry = ordered[index];
            var position = Math.Clamp(
                entry.Time.TotalSeconds / maximumTime.TotalSeconds,
                0,
                1);
            var lane = Enumerable.Range(0, laneCount)
                .Reverse()
                .FirstOrDefault(
                    candidate => position - lastPositionByLane[candidate] >= minimumMarkerDistance,
                    -1);
            if (lane < 0)
            {
                lane = Array.IndexOf(lastPositionByLane, lastPositionByLane.Min());
            }

            lastPositionByLane[lane] = position;
            markers.Add(new MapControlTimelineMarkerViewModel(
                position,
                6 + lane * 28,
                MapControlContentText.FormatTime(entry.Time),
                entry.Title,
                entry.IsUserReminder));
        }

        return markers;
    }

    private MapControlVerificationIssueViewModel CreateIssue(
        MapControlVerificationIssueSeverity severity,
        string resourceKey) =>
        new(severity, _localization.Get(resourceKey));

    private static TimeSpan RoundUpToFiveMinutes(TimeSpan value)
    {
        const int stepMinutes = 5;
        var minutes = (int)Math.Ceiling(value.TotalMinutes / stepMinutes) * stepMinutes;
        return TimeSpan.FromMinutes(minutes);
    }

    private void NotifyStatusChanged()
    {
        OnPropertyChanged(nameof(CanContinue));
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(StatusDescription));
        OnPropertyChanged(nameof(ErrorCountLabel));
        OnPropertyChanged(nameof(WarningCountLabel));
    }

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(RuleCount));
        OnPropertyChanged(nameof(VisionRuleCount));
        OnPropertyChanged(nameof(HasVisionCheck));
        OnPropertyChanged(nameof(CheckPositionOptions));
        OnPropertyChanged(nameof(CheckTransitionOptions));
        OnPropertyChanged(nameof(CheckResult));
        OnPropertyChanged(nameof(TimedReminderCount));
        OnPropertyChanged(nameof(PersistentTipCount));
    }

    private void NotifyLocalizedTextChanged()
    {
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(StatusDescription));
        OnPropertyChanged(nameof(ErrorCountLabel));
        OnPropertyChanged(nameof(WarningCountLabel));
    }
}

public enum MapControlVerificationIssueSeverity
{
    Warning,
    Error
}

public sealed record MapControlVerificationIssueViewModel(
    MapControlVerificationIssueSeverity Severity,
    string Message);

public sealed record MapControlTimelineMarkerViewModel(
    double PositionRatio,
    double LaneTop,
    string TimeLabel,
    string Title,
    bool IsUserReminder);

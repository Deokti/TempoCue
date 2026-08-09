using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>
/// Состояние библиотеки коучинга. На первой итерации показывает встроенный
/// матчап; операции импорта, хранения и редактирования будут отдельными сценариями.
/// </summary>
public sealed class CoachingViewModel : ObservableObject, IDisposable
{
    private readonly CoachPack _pack;
    private readonly LocalizationService _localization;

    private IReadOnlyList<CoachingRuleViewModel> _rules = [];
    private string _rulesSummary = string.Empty;
    private string _patchSummary = string.Empty;
    private string _patchWarningDescription = string.Empty;
    private CoachingScreen _currentScreen = CoachingScreen.Library;
    private bool _isDisposed;

    public CoachingViewModel(CoachPack pack, LocalizationService localization)
    {
        _pack = pack;
        _localization = localization;
        _localization.PropertyChanged += Localization_PropertyChanged;

        var firstRule = pack.Rules.FirstOrDefault();
        PlayerChampionInitials = GetInitials(firstRule?.PlayerChampion);
        OpponentChampionInitials = GetInitials(firstRule?.OpponentChampion);
        PackTypeSelection = new PackTypeSelectionViewModel();
        MatchupEditor = new MatchupEditorViewModel(localization.CurrentLanguageCode);
        RenderLocalizedPresentation();
    }

    public PackTypeSelectionViewModel PackTypeSelection { get; }

    public MatchupEditorViewModel MatchupEditor { get; }

    public CoachingScreen CurrentScreen
    {
        get => _currentScreen;
        private set => SetProperty(ref _currentScreen, value);
    }

    public string PackName => _pack.Name;

    public string PlayerChampionInitials { get; }

    public string OpponentChampionInitials { get; }

    public IReadOnlyList<CoachingRuleViewModel> Rules
    {
        get => _rules;
        private set => SetProperty(ref _rules, value);
    }

    public string RulesSummary
    {
        get => _rulesSummary;
        private set => SetProperty(ref _rulesSummary, value);
    }

    public string PatchSummary
    {
        get => _patchSummary;
        private set => SetProperty(ref _patchSummary, value);
    }

    public string PatchWarningDescription
    {
        get => _patchWarningDescription;
        private set => SetProperty(ref _patchWarningDescription, value);
    }

    public void OpenPackTypeSelection() =>
        CurrentScreen = CoachingScreen.PackTypeSelection;

    public void OpenMatchupEditor() =>
        CurrentScreen = CoachingScreen.MatchupEditor;

    public void OpenLibrary() =>
        CurrentScreen = CoachingScreen.Library;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _localization.PropertyChanged -= Localization_PropertyChanged;
    }

    private void Localization_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        RenderLocalizedPresentation();

    private void RenderLocalizedPresentation()
    {
        RulesSummary = _localization.Format("Coaching.Pack.Rules", _pack.Rules.Count);
        PatchSummary = _localization.Format("Coaching.Pack.Patch", _pack.Patch);
        PatchWarningDescription = _localization.Format(
            "Coaching.Update.WarningDescription",
            _pack.Patch);
        Rules = _pack.Rules
            .Select(CreateRuleViewModel)
            .ToArray();
    }

    private CoachingRuleViewModel CreateRuleViewModel(CoachRuleDefinition rule)
    {
        var powerStateKey = rule.PowerState switch
        {
            PowerState.Dangerous => "Coaching.Power.Dangerous",
            PowerState.Cautious => "Coaching.Power.Cautious",
            PowerState.Playable => "Coaching.Power.Playable",
            PowerState.Favorable => "Coaching.Power.Favorable",
            _ => throw new ArgumentOutOfRangeException(nameof(rule.PowerState))
        };
        var levelRange = rule.MaximumLevel is int maximumLevel
            ? _localization.Format(
                "Coaching.Rule.LevelRange",
                rule.MinimumLevel,
                maximumLevel)
            : _localization.Format("Coaching.Rule.LevelFrom", rule.MinimumLevel);

        return new CoachingRuleViewModel(
            rule.PowerState,
            _localization.Get(powerStateKey),
            levelRange,
            rule.Title,
            rule.Message,
            _localization.Format("Coaching.Rule.Source", rule.Source));
    }

    private static string GetInitials(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "—";
        }

        return new string(value
            .Where(char.IsLetterOrDigit)
            .Take(3)
            .Select(char.ToUpperInvariant)
            .ToArray());
    }
}

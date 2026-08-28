using TempoCue.App.Localization;
using TempoCue.Coaching;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Один вариант роли соперника в редакторе правила наблюдения.</summary>
public sealed record MapControlVisionPositionOptionViewModel(
    PlayerPosition Value,
    string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>Один вариант события видимости в редакторе правила наблюдения.</summary>
public sealed record MapControlVisionTransitionOptionViewModel(
    MapVisionTransition Value,
    string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// Хранит изолированную рабочую копию правила наблюдения.
/// </summary>
/// <remarks>
/// Проще редактора правил по объектам ровно на два поля: у наблюдения нет ни
/// упреждения, ни повторяемости. Событие уже случилось — предупреждать заранее
/// не о чем, и циклов у него не бывает.
/// </remarks>
public sealed class MapControlVisionRuleEditorViewModel : ObservableObject
{
    /// <summary>
    /// Роли, о которых можно уведомлять. Unknown в списке нет намеренно: роль
    /// приходит из Live Client Data, и правило «о ком угодно» срабатывало бы на
    /// случайном участнике вместо названного.
    /// </summary>
    private static readonly PlayerPosition[] SelectablePositions =
    [
        PlayerPosition.Top,
        PlayerPosition.Jungle,
        PlayerPosition.Middle,
        PlayerPosition.Bottom,
        PlayerPosition.Support
    ];

    private readonly LocalizationService _localization;
    private string? _editingRuleId;
    private PlayerPosition _selectedPosition = PlayerPosition.Jungle;
    private MapVisionTransition _selectedTransition = MapVisionTransition.Appeared;
    private MapObjectiveReminderPriority _selectedPriority = MapObjectiveReminderPriority.Caution;
    private string _message = string.Empty;
    private string _whyNow = string.Empty;
    private string _nextGoal = string.Empty;
    private bool _isEnabled = true;
    private IReadOnlyList<MapControlVisionPositionOptionViewModel> _positionOptions = [];
    private IReadOnlyList<MapControlVisionTransitionOptionViewModel> _transitionOptions = [];
    private IReadOnlyList<MapControlRulePriorityOptionViewModel> _priorityOptions = [];
    private bool _isOpen;

    public MapControlVisionRuleEditorViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        RebuildOptions();
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public string? EditingRuleId
    {
        get => _editingRuleId;
        private set => SetProperty(ref _editingRuleId, value);
    }

    public PlayerPosition SelectedPosition
    {
        get => _selectedPosition;
        set
        {
            if (SetProperty(ref _selectedPosition, value))
            {
                OnPropertyChanged(nameof(PreviewText));
            }
        }
    }

    public MapVisionTransition SelectedTransition
    {
        get => _selectedTransition;
        set
        {
            if (SetProperty(ref _selectedTransition, value))
            {
                OnPropertyChanged(nameof(PreviewText));
            }
        }
    }

    public MapObjectiveReminderPriority SelectedPriority
    {
        get => _selectedPriority;
        set => SetProperty(ref _selectedPriority, value);
    }

    public IReadOnlyList<MapControlVisionPositionOptionViewModel> PositionOptions
    {
        get => _positionOptions;
        private set => SetProperty(ref _positionOptions, value);
    }

    public IReadOnlyList<MapControlVisionTransitionOptionViewModel> TransitionOptions
    {
        get => _transitionOptions;
        private set => SetProperty(ref _transitionOptions, value);
    }

    public IReadOnlyList<MapControlRulePriorityOptionViewModel> PriorityOptions
    {
        get => _priorityOptions;
        private set => SetProperty(ref _priorityOptions, value);
    }

    /// <summary>
    /// «Сработает, когда наблюдение зафиксирует: лес — появился на карте».
    /// Условие правила состоит из двух выборов, и предпросмотр показывает их
    /// одной фразой — прежде, чем правило впервые сработает в бою.
    /// </summary>
    public string PreviewText => _localization.Format(
        "Coaching.MapControlEditor.Content.Vision.Preview",
        MapControlContentText.VisionPositionName(SelectedPosition),
        MapControlContentText.VisionTransitionName(SelectedTransition, _localization));

    public string Message
    {
        get => _message;
        set
        {
            if (SetProperty(ref _message, value ?? string.Empty))
            {
                NotifyValidationChanged(nameof(MessageLengthFeedback));
            }
        }
    }

    public string WhyNow
    {
        get => _whyNow;
        set
        {
            if (SetProperty(ref _whyNow, value ?? string.Empty))
            {
                NotifyValidationChanged(nameof(WhyNowLengthFeedback));
            }
        }
    }

    public string NextGoal
    {
        get => _nextGoal;
        set
        {
            if (SetProperty(ref _nextGoal, value ?? string.Empty))
            {
                NotifyValidationChanged(nameof(NextGoalLengthFeedback));
            }
        }
    }

    public TextLengthFeedback MessageLengthFeedback => TextLengthFeedback.Create(
        Message, 3, 240, _localization);

    public TextLengthFeedback WhyNowLengthFeedback => TextLengthFeedback.Create(
        WhyNow, 3, 240, _localization);

    /// <summary>
    /// «Следующая цель» обязательна, в отличие от напоминаний по времени:
    /// проверка набора требует её у каждого правила наблюдения.
    /// </summary>
    public TextLengthFeedback NextGoalLengthFeedback => TextLengthFeedback.Create(
        NextGoal, 3, 240, _localization);

    public bool CanSave =>
        TextLengthFeedback.IsWithin(Message, 3, 240)
        && TextLengthFeedback.IsWithin(WhyNow, 3, 240)
        && TextLengthFeedback.IsWithin(NextGoal, 3, 240);

    public void BeginNew()
    {
        EditingRuleId = null;
        SelectedPosition = PlayerPosition.Jungle;
        SelectedTransition = MapVisionTransition.Appeared;
        SelectedPriority = MapObjectiveReminderPriority.Caution;
        Message = string.Empty;
        WhyNow = string.Empty;
        NextGoal = string.Empty;
        _isEnabled = true;
        IsOpen = true;
    }

    public void BeginEdit(MapVisionRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        EditingRuleId = rule.Id;
        SelectedPosition = rule.Position;
        SelectedTransition = rule.Transition;
        SelectedPriority = rule.Priority;
        Message = rule.Message;
        WhyNow = rule.WhyNow;
        NextGoal = rule.NextGoal;
        // Выключателя в редакторе нет, но и включать выключенное правило молча
        // нельзя: состояние переживает правку.
        _isEnabled = rule.IsEnabled;
        IsOpen = true;
    }

    public MapVisionRule CreateRule(string id)
    {
        if (!CanSave)
        {
            throw new InvalidOperationException(
                "Нельзя сохранить незаполненное правило наблюдения.");
        }

        var rule = new MapVisionRule(
            id,
            SelectedPosition,
            SelectedTransition,
            SelectedPriority,
            Message.Trim(),
            WhyNow.Trim(),
            NextGoal.Trim(),
            _isEnabled);
        MapVisionRuleValidator.Validate(rule);
        return rule;
    }

    public void Close() => IsOpen = false;

    public void UpdateLanguage()
    {
        RebuildOptions();
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(MessageLengthFeedback));
        OnPropertyChanged(nameof(WhyNowLengthFeedback));
        OnPropertyChanged(nameof(NextGoalLengthFeedback));
    }

    private void RebuildOptions()
    {
        PositionOptions = SelectablePositions
            .Select(position => new MapControlVisionPositionOptionViewModel(
                position,
                MapControlContentText.VisionPositionName(position)))
            .ToArray();
        TransitionOptions = Enum.GetValues<MapVisionTransition>()
            .Select(transition => new MapControlVisionTransitionOptionViewModel(
                transition,
                MapControlContentText.VisionTransitionName(transition, _localization)))
            .ToArray();
        PriorityOptions = Enum.GetValues<MapObjectiveReminderPriority>()
            .Select(priority => new MapControlRulePriorityOptionViewModel(
                priority,
                MapControlContentText.PriorityName(priority, _localization)))
            .ToArray();
    }

    private void NotifyValidationChanged(string feedbackPropertyName)
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(feedbackPropertyName);
    }
}

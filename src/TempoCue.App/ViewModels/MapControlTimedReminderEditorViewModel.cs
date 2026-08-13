using TempoCue.App.Localization;
using TempoCue.Coaching;

namespace TempoCue.App.ViewModels;

/// <summary>Хранит изолированную рабочую копию напоминания по игровому времени.</summary>
public sealed class MapControlTimedReminderEditorViewModel : ObservableObject
{
    private static readonly TimeSpan TimeStep = TimeSpan.FromSeconds(15);

    private readonly LocalizationService _localization;
    private string? _editingReminderId;
    private TimeSpan _gameTime = TimeSpan.FromMinutes(3) + TimeStep;
    private MapObjectiveReminderPriority _selectedPriority = MapObjectiveReminderPriority.Caution;
    private string _title = string.Empty;
    private string _message = string.Empty;
    private string _whyNow = string.Empty;
    private string _nextGoal = string.Empty;
    private IReadOnlyList<MapControlRulePriorityOptionViewModel> _priorityOptions = [];
    private bool _isOpen;

    public MapControlTimedReminderEditorViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        RebuildPriorityOptions();
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public string? EditingReminderId
    {
        get => _editingReminderId;
        private set => SetProperty(ref _editingReminderId, value);
    }

    public TimeSpan GameTime
    {
        get => _gameTime;
        private set
        {
            if (SetProperty(ref _gameTime, value))
            {
                OnPropertyChanged(nameof(GameTimeLabel));
                OnPropertyChanged(nameof(GameTimeInput));
                OnPropertyChanged(nameof(CanDecreaseGameTime));
                OnPropertyChanged(nameof(CanIncreaseGameTime));
            }
        }
    }

    public string GameTimeLabel => MapControlContentText.FormatTime(GameTime);

    public string GameTimeInput
    {
        get => GameTimeLabel;
        set
        {
            if (TryParseGameTime(value, out var gameTime))
            {
                GameTime = gameTime;
            }

            OnPropertyChanged();
        }
    }

    public bool CanDecreaseGameTime => GameTime > TimeSpan.Zero;

    public bool CanIncreaseGameTime =>
        GameTime < MapControlTimedReminderValidator.MaximumGameTime;

    public MapObjectiveReminderPriority SelectedPriority
    {
        get => _selectedPriority;
        set => SetProperty(ref _selectedPriority, value);
    }

    public IReadOnlyList<MapControlRulePriorityOptionViewModel> PriorityOptions
    {
        get => _priorityOptions;
        private set => SetProperty(ref _priorityOptions, value);
    }

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value ?? string.Empty))
            {
                NotifyValidationChanged(nameof(TitleLengthFeedback));
            }
        }
    }

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

    public TextLengthFeedback TitleLengthFeedback => TextLengthFeedback.Create(
        Title, 3, 80, _localization);

    public TextLengthFeedback MessageLengthFeedback => TextLengthFeedback.Create(
        Message, 3, 240, _localization);

    public TextLengthFeedback WhyNowLengthFeedback => TextLengthFeedback.Create(
        WhyNow, 3, 240, _localization);

    public TextLengthFeedback NextGoalLengthFeedback => TextLengthFeedback.CreateOptional(
        NextGoal, 240, _localization);

    public bool CanSave =>
        TextLengthFeedback.IsWithin(Title, 3, 80)
        && TextLengthFeedback.IsWithin(Message, 3, 240)
        && TextLengthFeedback.IsWithin(WhyNow, 3, 240)
        && TextLengthFeedback.IsOptionalWithin(NextGoal, 240);

    public void BeginNew()
    {
        EditingReminderId = null;
        GameTime = TimeSpan.FromMinutes(3) + TimeStep;
        SelectedPriority = MapObjectiveReminderPriority.Caution;
        Title = string.Empty;
        Message = string.Empty;
        WhyNow = string.Empty;
        NextGoal = string.Empty;
        IsOpen = true;
    }

    public void BeginEdit(MapControlTimedReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        EditingReminderId = reminder.Id;
        GameTime = reminder.GameTime;
        SelectedPriority = reminder.Priority;
        Title = reminder.Title;
        Message = reminder.Message;
        WhyNow = reminder.WhyNow;
        NextGoal = reminder.NextGoal;
        IsOpen = true;
    }

    public void IncreaseGameTime()
    {
        if (CanIncreaseGameTime)
        {
            GameTime += TimeStep;
        }
    }

    public void DecreaseGameTime()
    {
        if (CanDecreaseGameTime)
        {
            GameTime -= TimeStep;
        }
    }

    public MapControlTimedReminder CreateReminder(string id)
    {
        if (!CanSave)
        {
            throw new InvalidOperationException(
                "Нельзя сохранить незаполненное напоминание по времени.");
        }

        var reminder = new MapControlTimedReminder
        {
            Id = id,
            GameTime = GameTime,
            Priority = SelectedPriority,
            Title = Title.Trim(),
            Message = Message.Trim(),
            WhyNow = WhyNow.Trim(),
            NextGoal = NextGoal.Trim()
        };
        MapControlTimedReminderValidator.Validate(reminder);
        return reminder;
    }

    public void Close() => IsOpen = false;

    public void UpdateLanguage()
    {
        RebuildPriorityOptions();
        OnPropertyChanged(nameof(TitleLengthFeedback));
        OnPropertyChanged(nameof(MessageLengthFeedback));
        OnPropertyChanged(nameof(WhyNowLengthFeedback));
        OnPropertyChanged(nameof(NextGoalLengthFeedback));
    }

    private void RebuildPriorityOptions()
    {
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

    private static bool TryParseGameTime(string? input, out TimeSpan gameTime)
    {
        gameTime = default;
        var digits = input?.Where(char.IsAsciiDigit).ToArray() ?? [];
        if (digits.Length is < 1 or > 4)
        {
            return false;
        }

        var digitText = new string(digits);
        var hasSeconds = digits.Length > 2;
        var minuteText = hasSeconds ? digitText[..^2] : digitText;
        var secondText = hasSeconds ? digitText[^2..] : "0";
        if (!int.TryParse(minuteText, out var minutes)
            || !int.TryParse(secondText, out var seconds)
            || seconds is < 0 or > 59
            || seconds % (int)TimeStep.TotalSeconds != 0)
        {
            return false;
        }

        gameTime = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
        return gameTime <= MapControlTimedReminderValidator.MaximumGameTime;
    }
}

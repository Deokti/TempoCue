using TempoCue.App.Localization;
using TempoCue.Coaching;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Хранит изолированную рабочую копию постоянной подсказки.</summary>
public sealed class MapControlPersistentTipEditorViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private string? _editingTipId;
    private string _title = string.Empty;
    private string _message = string.Empty;
    private MapControlPersistentTipScope _selectedScope =
        new(MapControlPersistentTipScopeKind.WholeMatch);
    private IReadOnlyList<MapControlPersistentTipScopeOptionViewModel> _scopeOptions = [];
    private bool _isOpen;

    public MapControlPersistentTipEditorViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        RebuildScopeOptions();
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public string? EditingTipId
    {
        get => _editingTipId;
        private set
        {
            if (SetProperty(ref _editingTipId, value))
            {
                OnPropertyChanged(nameof(IsEditingExisting));
                OnPropertyChanged(nameof(Header));
            }
        }
    }

    public bool IsEditingExisting => EditingTipId is not null;

    public string Header => _localization.Get(IsEditingExisting
        ? "Coaching.MapControlEditor.Content.Persistent.Editor.EditTitle"
        : "Coaching.MapControlEditor.Content.Persistent.Editor.AddTitle");

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

    public MapControlPersistentTipScope SelectedScope
    {
        get => _selectedScope;
        set => SetProperty(ref _selectedScope, value ?? new(
            MapControlPersistentTipScopeKind.WholeMatch));
    }

    public IReadOnlyList<MapControlPersistentTipScopeOptionViewModel> ScopeOptions
    {
        get => _scopeOptions;
        private set => SetProperty(ref _scopeOptions, value);
    }

    public TextLengthFeedback TitleLengthFeedback => TextLengthFeedback.Create(
        Title,
        3,
        80,
        _localization);

    public TextLengthFeedback MessageLengthFeedback => TextLengthFeedback.Create(
        Message,
        3,
        240,
        _localization);

    public bool CanSave =>
        TextLengthFeedback.IsWithin(Title, 3, 80)
        && TextLengthFeedback.IsWithin(Message, 3, 240);

    public void BeginNew()
    {
        EditingTipId = null;
        Title = string.Empty;
        Message = string.Empty;
        SelectedScope = new(MapControlPersistentTipScopeKind.WholeMatch);
        IsOpen = true;
    }

    public void BeginEdit(MapControlPersistentTip tip)
    {
        ArgumentNullException.ThrowIfNull(tip);
        EditingTipId = tip.Id;
        Title = tip.Title;
        Message = tip.Message;
        SelectedScope = tip.Scope;
        IsOpen = true;
    }

    public MapControlPersistentTip CreateTip(string id)
    {
        if (!CanSave)
        {
            throw new InvalidOperationException(
                "Нельзя сохранить незаполненную постоянную подсказку.");
        }

        var tip = new MapControlPersistentTip
        {
            Id = id,
            Title = Title.Trim(),
            Message = Message.Trim(),
            Scope = SelectedScope
        };
        MapControlPersistentTipValidator.Validate(tip);
        return tip;
    }

    public void Close() => IsOpen = false;

    public void UpdateLanguage()
    {
        RebuildScopeOptions();
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(TitleLengthFeedback));
        OnPropertyChanged(nameof(MessageLengthFeedback));
    }

    private void RebuildScopeOptions()
    {
        ScopeOptions =
        [
            CreateScopeOption(MapControlPersistentTipScopeKind.WholeMatch),
            CreateScopeOption(MapControlPersistentTipScopeKind.BeforeFirstObjective),
            CreateScopeOption(MapControlPersistentTipScopeKind.WhileAnyObjectiveAvailable),
            CreateObjectiveScopeOption(MapObjectiveKind.ElementalDragon),
            CreateObjectiveScopeOption(MapObjectiveKind.VoidGrubs),
            CreateObjectiveScopeOption(MapObjectiveKind.RiftHerald),
            CreateObjectiveScopeOption(MapObjectiveKind.BaronNashor)
        ];
    }

    private MapControlPersistentTipScopeOptionViewModel CreateScopeOption(
        MapControlPersistentTipScopeKind kind)
    {
        var scope = new MapControlPersistentTipScope(kind);
        return new(scope, MapControlContentText.PersistentTipScopeName(scope, _localization));
    }

    private MapControlPersistentTipScopeOptionViewModel CreateObjectiveScopeOption(
        MapObjectiveKind objective)
    {
        var scope = new MapControlPersistentTipScope(
            MapControlPersistentTipScopeKind.SpecificObjective,
            objective);
        return new(scope, MapControlContentText.PersistentTipScopeName(scope, _localization));
    }

    private void NotifyValidationChanged(string feedbackPropertyName)
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(feedbackPropertyName);
    }
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Coaching;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Координирует независимые разделы содержимого набора контроля карты.</summary>
public sealed class MapControlContentViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private MapControlContentSection _section;

    public MapControlContentViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        ObjectiveRules = new MapControlObjectiveRulesViewModel(localization);
        TimedReminders = new MapControlTimedRemindersViewModel(localization);
        PersistentTips = new MapControlPersistentTipsViewModel(localization);
        ObjectiveRules.PropertyChanged += ObjectiveRules_PropertyChanged;
        TimedReminders.PropertyChanged += TimedReminders_PropertyChanged;
        PersistentTips.PropertyChanged += PersistentTips_PropertyChanged;
    }

    public MapControlObjectiveRulesViewModel ObjectiveRules { get; }

    public MapControlTimedRemindersViewModel TimedReminders { get; }

    public MapControlPersistentTipsViewModel PersistentTips { get; }

    /// <summary>
    /// Правила наблюдения набора. Редактор их пока не показывает и не меняет,
    /// но обязан пронести через себя нетронутыми: без этого правка копии набора
    /// про лесника молча стирала бы единственное, ради чего набор существует.
    /// </summary>
    public IReadOnlyList<MapVisionRule> VisionRules { get; private set; } = [];

    // Совместимые свойства сохраняют существующие binding и сценарии редактора.
    public ReadOnlyObservableCollection<MapControlObjectiveRuleViewModel> Rules =>
        ObjectiveRules.Rules;

    public MapControlObjectiveRuleEditorViewModel Editor => ObjectiveRules.Editor;

    public IReadOnlyList<MapControlObjectiveOptionViewModel> ObjectiveOptions =>
        ObjectiveRules.ObjectiveOptions;

    public bool IsObjectivePickerOpen => ObjectiveRules.IsObjectivePickerOpen;

    public string RulesDescription => ObjectiveRules.RulesDescription;

    public MapControlContentSection Section
    {
        get => _section;
        private set
        {
            if (SetProperty(ref _section, value))
            {
                OnPropertyChanged(nameof(IsObjectsSection));
                OnPropertyChanged(nameof(IsTimedRemindersSection));
                OnPropertyChanged(nameof(IsPersistentTipsSection));
            }
        }
    }

    public bool IsObjectsSection => Section == MapControlContentSection.Objects;

    public bool IsTimedRemindersSection => Section == MapControlContentSection.TimedReminders;

    public bool IsPersistentTipsSection => Section == MapControlContentSection.PersistentTips;

    public int RuleCount => ObjectiveRules.RuleCount;

    public bool HasRules => ObjectiveRules.HasRules;

    public bool HasContent =>
        HasRules
        || TimedReminders.Count > 0
        || PersistentTips.Count > 0
        // Набор из одних правил наблюдения — обычный набор, а не пустой: оба
        // встроенных набора наблюдения устроены именно так.
        || VisionRules.Count > 0;

    public bool IsComplete =>
        HasContent
        && ObjectiveRules.IsComplete
        && TimedReminders.IsComplete
        && PersistentTips.IsComplete;

    public string ObjectsTabLabel => _localization.Format(
        "Coaching.MapControlEditor.Content.Tab.Objects",
        RuleCount);

    public string TimedRemindersTabLabel => _localization.Format(
        "Coaching.MapControlEditor.Content.Tab.Timed",
        TimedReminders.Count);

    public string PersistentTipsTabLabel => _localization.Format(
        "Coaching.MapControlEditor.Content.Tab.Persistent",
        PersistentTips.Count);

    public void SetPatch(string patch)
    {
        ObjectiveRules.SetPatch(patch);
        NotifyObjectiveRuleBindings();
    }

    public void OpenObjectivePicker() => ObjectiveRules.OpenObjectivePicker();

    public void CloseObjectivePicker() => ObjectiveRules.CloseObjectivePicker();

    public void BeginAddRule(MapObjectiveKind objective) => ObjectiveRules.BeginAddRule(objective);

    public void BeginEditRule(MapControlObjectiveRuleViewModel rule) =>
        ObjectiveRules.BeginEditRule(rule);

    public bool SaveEditor() => ObjectiveRules.SaveEditor();

    public void CancelEditor() => ObjectiveRules.CancelEditor();

    public bool DuplicateEditor() => ObjectiveRules.DuplicateEditor();

    public bool DeleteEditor() => ObjectiveRules.DeleteEditor();

    public bool DuplicateRule(MapControlObjectiveRuleViewModel source) =>
        ObjectiveRules.DuplicateRule(source);

    public bool DeleteRule(MapControlObjectiveRuleViewModel rule) =>
        ObjectiveRules.DeleteRule(rule);

    public bool MoveRule(MapControlObjectiveRuleViewModel rule, int offset) =>
        ObjectiveRules.MoveRule(rule, offset);

    public void ShowObjects()
    {
        CloseObjectivePicker();
        Section = MapControlContentSection.Objects;
    }

    public void ShowPersistentTips()
    {
        CloseObjectivePicker();
        Section = MapControlContentSection.PersistentTips;
    }

    public void ShowTimedReminders()
    {
        CloseObjectivePicker();
        Section = MapControlContentSection.TimedReminders;
    }

    public void BeginNew()
    {
        ObjectiveRules.BeginNew();
        Section = MapControlContentSection.Objects;
        TimedReminders.BeginNew();
        PersistentTips.BeginNew();
        VisionRules = [];
        NotifyObjectiveRuleBindings();
    }

    public void Load(MapControlPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ObjectiveRules.Load(pack.Rules);
        TimedReminders.Load(pack.TimedReminders);
        PersistentTips.Load(pack.PersistentTips);
        VisionRules = pack.VisionRules;
        Section = MapControlContentSection.Objects;
        NotifyObjectiveRuleBindings();
        OnPropertyChanged(nameof(HasContent));
        OnPropertyChanged(nameof(IsComplete));
    }

    public void UpdateLanguage()
    {
        ObjectiveRules.UpdateLanguage();
        TimedReminders.UpdateLanguage();
        PersistentTips.UpdateLanguage();
        OnPropertyChanged(nameof(ObjectsTabLabel));
        OnPropertyChanged(nameof(TimedRemindersTabLabel));
        OnPropertyChanged(nameof(PersistentTipsTabLabel));
        NotifyObjectiveRuleBindings();
    }

    private void ObjectiveRules_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapControlObjectiveRulesViewModel.ObjectiveOptions))
        {
            OnPropertyChanged(nameof(ObjectiveOptions));
        }

        if (e.PropertyName == nameof(MapControlObjectiveRulesViewModel.IsObjectivePickerOpen))
        {
            OnPropertyChanged(nameof(IsObjectivePickerOpen));
        }

        if (e.PropertyName == nameof(MapControlObjectiveRulesViewModel.RulesDescription))
        {
            OnPropertyChanged(nameof(RulesDescription));
        }

        if (e.PropertyName == nameof(MapControlObjectiveRulesViewModel.RuleCount))
        {
            NotifyObjectiveRuleCountChanged();
        }

        if (e.PropertyName == nameof(MapControlObjectiveRulesViewModel.IsComplete))
        {
            OnPropertyChanged(nameof(IsComplete));
        }
    }

    private void PersistentTips_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapControlPersistentTipsViewModel.Count))
        {
            OnPropertyChanged(nameof(PersistentTipsTabLabel));
            OnPropertyChanged(nameof(HasContent));
            OnPropertyChanged(nameof(IsComplete));
        }

        if (e.PropertyName == nameof(MapControlPersistentTipsViewModel.IsComplete))
        {
            OnPropertyChanged(nameof(IsComplete));
        }
    }

    private void TimedReminders_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapControlTimedRemindersViewModel.Count))
        {
            OnPropertyChanged(nameof(TimedRemindersTabLabel));
            OnPropertyChanged(nameof(HasContent));
            OnPropertyChanged(nameof(IsComplete));
        }

        if (e.PropertyName == nameof(MapControlTimedRemindersViewModel.IsComplete))
        {
            OnPropertyChanged(nameof(IsComplete));
        }
    }

    private void NotifyObjectiveRuleCountChanged()
    {
        OnPropertyChanged(nameof(RuleCount));
        OnPropertyChanged(nameof(HasRules));
        OnPropertyChanged(nameof(HasContent));
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(ObjectsTabLabel));
    }

    private void NotifyObjectiveRuleBindings()
    {
        OnPropertyChanged(nameof(ObjectiveOptions));
        OnPropertyChanged(nameof(IsObjectivePickerOpen));
        OnPropertyChanged(nameof(RulesDescription));
        NotifyObjectiveRuleCountChanged();
    }
}

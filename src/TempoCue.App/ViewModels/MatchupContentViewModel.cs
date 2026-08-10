using System.Collections.ObjectModel;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>
/// Единственный владелец фаз в черновике матчапа и операций над их порядком.
/// </summary>
public sealed class MatchupContentViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly ObservableCollection<MatchupPhaseViewModel> _phases = [];
    private MatchupContentSection _currentSection = MatchupContentSection.Phases;
    private int _nextPhaseNumber = 1;

    public MatchupContentViewModel(
        LocalizationService localization,
        IItemCatalog itemCatalog)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        ArgumentNullException.ThrowIfNull(itemCatalog);
        Phases = new ReadOnlyObservableCollection<MatchupPhaseViewModel>(_phases);
        Editor = new MatchupPhaseEditorViewModel(localization);
        PersistentTips = new MatchupPersistentTipsViewModel(localization);
        PersistentTips.PropertyChanged += PersistentTips_PropertyChanged;
        ItemPicker = new ItemPickerViewModel(itemCatalog, localization);
        Validation = new MatchupPhaseValidationViewModel(
            localization,
            new MatchupPhaseSetValidator());
        ItemPicker.ItemsSelected += ItemPicker_ItemsSelected;
    }

    public ReadOnlyObservableCollection<MatchupPhaseViewModel> Phases { get; }

    public MatchupPhaseEditorViewModel Editor { get; }

    public MatchupPersistentTipsViewModel PersistentTips { get; }

    public ItemPickerViewModel ItemPicker { get; }

    public MatchupPhaseValidationViewModel Validation { get; }

    public bool HasPhases => _phases.Count > 0;

    public int PhaseCount => _phases.Count;

    public bool IsComplete => HasPhases && !Validation.HasErrors;

    public MatchupContentSection CurrentSection
    {
        get => _currentSection;
        private set
        {
            if (SetProperty(ref _currentSection, value))
            {
                OnPropertyChanged(nameof(IsPhasesSection));
                OnPropertyChanged(nameof(IsPersistentTipsSection));
            }
        }
    }

    public bool IsPhasesSection => CurrentSection == MatchupContentSection.Phases;

    public bool IsPersistentTipsSection => CurrentSection == MatchupContentSection.PersistentTips;

    public string PhasesTabLabel =>
        _localization.Format("Coaching.MatchupEditor.Content.Tab.Phases", PhaseCount);

    public string PersistentTipsTabLabel =>
        _localization.Format(
            "Coaching.MatchupEditor.Content.Tab.Persistent",
            PersistentTips.Count);

    public void ShowPhases() => CurrentSection = MatchupContentSection.Phases;

    public void ShowPersistentTips() => CurrentSection = MatchupContentSection.PersistentTips;

    public void BeginAddPhase() => Editor.BeginNew();

    public void SetItemCatalogContext(string gamePatch, string languageCode) =>
        ItemPicker.SetContext(gamePatch, languageCode);

    public Task EnsureItemCatalogLoadedAsync(CancellationToken cancellationToken = default) =>
        ItemPicker.EnsureCatalogLoadedAsync(cancellationToken);

    public Task RetryItemCatalogLoadAsync(CancellationToken cancellationToken = default) =>
        ItemPicker.RetryCatalogLoadAsync(cancellationToken);

    public void OpenRecommendedItemPicker() => ItemPicker.Open(
        ItemPickerTarget.PhaseRecommendations,
        Editor.RecommendedItems.Select(static item => item.Id));

    public void RemoveRecommendedItem(MatchupItemDraft item) =>
        Editor.RemoveRecommendedItem(item);

    public void BeginEditPhase(MatchupPhaseViewModel phase)
    {
        ArgumentNullException.ThrowIfNull(phase);
        Editor.BeginEdit(phase.CreateSnapshot());
    }

    public bool SaveEditor()
    {
        if (!Editor.CanSave)
        {
            return false;
        }

        if (Editor.EditingPhaseId is string editingPhaseId)
        {
            var existingPhase = _phases.FirstOrDefault(phase => phase.Id == editingPhaseId);
            if (existingPhase is null)
            {
                return false;
            }

            existingPhase.Replace(Editor.CreateSnapshot(editingPhaseId));
        }
        else
        {
            var phaseId = CreatePhaseId();
            _phases.Add(new MatchupPhaseViewModel(
                Editor.CreateSnapshot(phaseId),
                _localization));
        }

        Editor.Close();
        NotifyCollectionChanged();
        return true;
    }

    public void CancelEditor() => Editor.Close();

    public MatchupPhaseViewModel? DuplicateEditor()
    {
        if (!Editor.CanSave)
        {
            return null;
        }

        MatchupPhaseDraft source;
        var sourceIndex = _phases.Count - 1;
        if (Editor.EditingPhaseId is string editingPhaseId)
        {
            sourceIndex = FindPhaseIndex(editingPhaseId);
            if (sourceIndex < 0)
            {
                return null;
            }

            source = Editor.CreateSnapshot(editingPhaseId);
        }
        else
        {
            source = Editor.CreateSnapshot(CreatePhaseId());
            _phases.Add(new MatchupPhaseViewModel(source, _localization));
            sourceIndex = _phases.Count - 1;
        }

        var copy = CreateDuplicate(source);
        _phases.Insert(sourceIndex + 1, copy);
        Editor.BeginEdit(copy.CreateSnapshot());
        NotifyCollectionChanged();
        return copy;
    }

    public bool DeleteEditor()
    {
        if (Editor.EditingPhaseId is not string editingPhaseId)
        {
            Editor.Close();
            return true;
        }

        var phaseIndex = FindPhaseIndex(editingPhaseId);
        return phaseIndex >= 0 && DeletePhase(_phases[phaseIndex]);
    }

    public MatchupPhaseViewModel DuplicatePhase(MatchupPhaseViewModel phase)
    {
        ArgumentNullException.ThrowIfNull(phase);
        var sourceIndex = _phases.IndexOf(phase);
        if (sourceIndex < 0)
        {
            throw new ArgumentException("Фаза не принадлежит текущему черновику.", nameof(phase));
        }

        var copy = CreateDuplicate(phase.CreateSnapshot());
        _phases.Insert(sourceIndex + 1, copy);
        NotifyCollectionChanged();
        return copy;
    }

    public bool DeletePhase(MatchupPhaseViewModel phase)
    {
        ArgumentNullException.ThrowIfNull(phase);
        var removed = _phases.Remove(phase);
        if (!removed)
        {
            return false;
        }

        if (Editor.EditingPhaseId == phase.Id)
        {
            Editor.Close();
        }

        NotifyCollectionChanged();
        return true;
    }

    public bool MovePhaseUp(MatchupPhaseViewModel phase) => MovePhase(phase, -1);

    public bool MovePhaseDown(MatchupPhaseViewModel phase) => MovePhase(phase, 1);

    public void UpdateLanguage(string languageCode)
    {
        Editor.UpdateLanguage();
        ItemPicker.UpdateLanguage(languageCode);
        foreach (var phase in _phases)
        {
            phase.UpdateLanguage();
        }

        Validation.UpdateLanguage();
        PersistentTips.UpdateLanguage();

        OnPropertyChanged(nameof(PhasesTabLabel));
        OnPropertyChanged(nameof(PersistentTipsTabLabel));
    }

    private void ItemPicker_ItemsSelected(
        ItemPickerTarget target,
        IReadOnlyList<ItemOptionViewModel> items)
    {
        if (target == ItemPickerTarget.PhaseRecommendations)
        {
            Editor.SetRecommendedItems(items);
        }
    }

    private bool MovePhase(MatchupPhaseViewModel phase, int offset)
    {
        ArgumentNullException.ThrowIfNull(phase);
        var currentIndex = _phases.IndexOf(phase);
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= _phases.Count)
        {
            return false;
        }

        _phases.Move(currentIndex, targetIndex);
        NotifyCollectionChanged();
        return true;
    }

    private MatchupPhaseViewModel CreateDuplicate(MatchupPhaseDraft source)
    {
        var duplicateName = _localization.Format(
            "Coaching.MatchupEditor.Content.DuplicateName",
            source.Name);
        return new MatchupPhaseViewModel(
            source with
            {
                Id = CreatePhaseId(),
                Name = duplicateName.Length <= 80
                    ? duplicateName
                    : duplicateName[..80].TrimEnd()
            },
            _localization);
    }

    private string CreatePhaseId() => $"phase-{_nextPhaseNumber++}";

    private int FindPhaseIndex(string phaseId)
    {
        for (var index = 0; index < _phases.Count; index++)
        {
            if (_phases[index].Id == phaseId)
            {
                return index;
            }
        }

        return -1;
    }

    private void NotifyCollectionChanged()
    {
        UpdatePositions();
        Validation.Update(_phases.Select(static phase => phase.CreateSnapshot()).ToArray());
        PersistentTips.UpdatePhases(_phases);
        OnPropertyChanged(nameof(HasPhases));
        OnPropertyChanged(nameof(PhaseCount));
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(PhasesTabLabel));
    }

    private void PersistentTips_PropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MatchupPersistentTipsViewModel.Count))
        {
            OnPropertyChanged(nameof(PersistentTipsTabLabel));
        }
    }

    private void UpdatePositions()
    {
        for (var index = 0; index < _phases.Count; index++)
        {
            _phases[index].UpdatePosition(index, _phases.Count);
        }
    }
}

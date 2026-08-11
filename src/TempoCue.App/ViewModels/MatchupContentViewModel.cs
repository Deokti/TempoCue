using System.Collections.ObjectModel;
using System.Globalization;
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
    private string? _itemPickerBlockId;
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
        ItemBlocks = new MatchupItemBlocksViewModel(localization);
        ItemBlocks.PropertyChanged += ItemBlocks_PropertyChanged;
        ItemPicker = new ItemPickerViewModel(itemCatalog, localization);
        Validation = new MatchupPhaseValidationViewModel(
            localization,
            new MatchupPhaseSetValidator());
        ItemPicker.ItemsSelected += ItemPicker_ItemsSelected;
    }

    public ReadOnlyObservableCollection<MatchupPhaseViewModel> Phases { get; }

    public MatchupPhaseEditorViewModel Editor { get; }

    public MatchupPersistentTipsViewModel PersistentTips { get; }

    public MatchupItemBlocksViewModel ItemBlocks { get; }

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
                OnPropertyChanged(nameof(IsItemsSection));
            }
        }
    }

    public bool IsPhasesSection => CurrentSection == MatchupContentSection.Phases;

    public bool IsPersistentTipsSection => CurrentSection == MatchupContentSection.PersistentTips;

    public bool IsItemsSection => CurrentSection == MatchupContentSection.Items;

    public string PhasesTabLabel =>
        _localization.Format("Coaching.MatchupEditor.Content.Tab.Phases", PhaseCount);

    public string PersistentTipsTabLabel =>
        _localization.Format(
            "Coaching.MatchupEditor.Content.Tab.Persistent",
            PersistentTips.Count);

    public string ItemsTabLabel =>
        _localization.Format(
            "Coaching.MatchupEditor.Content.Tab.Items",
            ItemBlocks.Count);

    public void ShowPhases() => CurrentSection = MatchupContentSection.Phases;

    public void ShowPersistentTips() => CurrentSection = MatchupContentSection.PersistentTips;

    public void ShowItems() => CurrentSection = MatchupContentSection.Items;

    public void BeginAddPhase() => Editor.BeginNew();

    public void SetItemCatalogContext(string gamePatch, string languageCode) =>
        ItemPicker.SetContext(gamePatch, languageCode);

    public Task EnsureItemCatalogLoadedAsync(CancellationToken cancellationToken = default) =>
        ItemPicker.EnsureCatalogLoadedAsync(cancellationToken);

    public Task RetryItemCatalogLoadAsync(CancellationToken cancellationToken = default) =>
        ItemPicker.RetryCatalogLoadAsync(cancellationToken);

    public void BeginNew()
    {
        Editor.Close();
        ItemPicker.Close();
        _itemPickerBlockId = null;
        _phases.Clear();
        _nextPhaseNumber = 1;
        PersistentTips.Load([]);
        ItemBlocks.Load([]);
        CurrentSection = MatchupContentSection.Phases;
        NotifyCollectionChanged();
    }

    public void Load(CoachPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        Editor.Close();
        ItemPicker.Close();
        _itemPickerBlockId = null;
        _phases.Clear();
        foreach (var rule in pack.Rules)
        {
            _phases.Add(new MatchupPhaseViewModel(
                new MatchupPhaseDraft(
                    rule.Id,
                    rule.Title,
                    rule.PowerState,
                    rule.MinimumLevel,
                    rule.MaximumLevel,
                    rule.Message,
                    rule.WhyNow,
                    rule.NextGoal,
                    CreateItemDrafts(rule.RecommendedItemIds, pack.Patch)),
                _localization));
        }

        _nextPhaseNumber = 1;
        NotifyCollectionChanged();
        PersistentTips.Load(pack.PersistentTips.Select(tip => new MatchupPersistentTipDraft(
            tip.Id,
            tip.Title,
            tip.Message,
            tip.ApplicableRuleId,
            string.IsNullOrWhiteSpace(tip.Source) ? null : tip.Source)));
        ItemBlocks.Load(pack.ItemBlocks.Select(block => new MatchupItemBlockDraft(
            block.Id,
            block.Name,
            block.WhenToChoose,
            CreateItemDrafts(block.ItemIds, pack.Patch))));
        CurrentSection = MatchupContentSection.Phases;
    }

    public void OpenRecommendedItemPicker() => ItemPicker.Open(
        ItemPickerTarget.PhaseRecommendations,
        Editor.RecommendedItems.Select(static item => item.Id));

    public void OpenEditedItemBlockPicker()
    {
        if (!ItemBlocks.Editor.IsOpen)
        {
            return;
        }

        _itemPickerBlockId = null;
        ItemPicker.Open(
            ItemPickerTarget.ItemBlockItems,
            ItemBlocks.Editor.Items.Select(static item => item.Id));
    }

    public void OpenItemBlockPicker(MatchupItemBlockViewModel block)
    {
        ArgumentNullException.ThrowIfNull(block);
        _itemPickerBlockId = block.Id;
        ItemPicker.Open(
            ItemPickerTarget.ItemBlockItems,
            block.Items.Select(static item => item.Id));
    }

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
        ItemBlocks.UpdateLanguage();

        OnPropertyChanged(nameof(PhasesTabLabel));
        OnPropertyChanged(nameof(PersistentTipsTabLabel));
        OnPropertyChanged(nameof(ItemsTabLabel));
    }

    private void ItemPicker_ItemsSelected(
        ItemPickerTarget target,
        IReadOnlyList<ItemOptionViewModel> items)
    {
        if (target == ItemPickerTarget.PhaseRecommendations)
        {
            Editor.SetRecommendedItems(items);
        }
        else if (target == ItemPickerTarget.ItemBlockItems)
        {
            if (_itemPickerBlockId is string blockId)
            {
                var block = ItemBlocks.Blocks.FirstOrDefault(item => item.Id == blockId);
                if (block is not null)
                {
                    ItemBlocks.SetItems(block, items);
                }
            }
            else if (ItemBlocks.Editor.IsOpen)
            {
                ItemBlocks.Editor.SetItems(items);
            }

            _itemPickerBlockId = null;
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

    private string CreatePhaseId()
    {
        string id;
        do
        {
            id = $"phase-{_nextPhaseNumber++}";
        }
        while (_phases.Any(phase => phase.Id == id));

        return id;
    }

    private IReadOnlyList<MatchupItemDraft> CreateItemDrafts(
        IEnumerable<int> itemIds,
        string patch)
    {
        var items = ItemPicker.Items.ToDictionary(static item => item.Id);
        return itemIds.Select(itemId => items.TryGetValue(itemId, out var item)
            ? new MatchupItemDraft(item.Id, item.DisplayName, item.ImageUri)
            : new MatchupItemDraft(
                itemId,
                itemId.ToString(CultureInfo.InvariantCulture),
                new Uri(
                    $"https://ddragon.leagueoflegends.com/cdn/{Uri.EscapeDataString(patch)}/img/item/{itemId}.png")))
            .ToArray();
    }

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

    private void ItemBlocks_PropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MatchupItemBlocksViewModel.Count))
        {
            OnPropertyChanged(nameof(ItemsTabLabel));
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

using TempoCue.App.Localization;

namespace TempoCue.App.ViewModels;

/// <summary>Изолированная рабочая копия нового или выбранного блока предметов.</summary>
public sealed class MatchupItemBlockEditorViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private bool _isOpen;
    private string? _editingBlockId;
    private string _name = string.Empty;
    private string _whenToChoose = string.Empty;
    private IReadOnlyList<MatchupItemDraft> _items = [];

    public MatchupItemBlockEditorViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public string? EditingBlockId
    {
        get => _editingBlockId;
        private set
        {
            if (SetProperty(ref _editingBlockId, value))
            {
                OnPropertyChanged(nameof(IsEditingExisting));
                OnPropertyChanged(nameof(Header));
            }
        }
    }

    public bool IsEditingExisting => EditingBlockId is not null;

    public string Header => IsEditingExisting
        ? _localization.Format("Coaching.MatchupEditor.Items.Editor.EditTitle", Name)
        : _localization.Get("Coaching.MatchupEditor.Items.Editor.AddTitle");

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(Header));
                OnPropertyChanged(nameof(NameLengthFeedback));
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    public string WhenToChoose
    {
        get => _whenToChoose;
        set
        {
            if (SetProperty(ref _whenToChoose, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    public IReadOnlyList<MatchupItemDraft> Items
    {
        get => _items;
        private set
        {
            if (SetProperty(ref _items, value))
            {
                OnPropertyChanged(nameof(HasItems));
                OnPropertyChanged(nameof(ItemCount));
            }
        }
    }

    public bool HasItems => Items.Count > 0;

    public int ItemCount => Items.Count;

    public TextLengthFeedback NameLengthFeedback => TextLengthFeedback.Create(
        Name,
        3,
        80,
        _localization);

    public bool CanSave =>
        TextLengthFeedback.IsWithin(Name, 3, 80)
        && WhenToChoose.Trim().Length <= 160;

    public void BeginNew()
    {
        EditingBlockId = null;
        Name = string.Empty;
        WhenToChoose = string.Empty;
        Items = [];
        IsOpen = true;
    }

    public void BeginEdit(MatchupItemBlockDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        EditingBlockId = draft.Id;
        Name = draft.Name;
        WhenToChoose = draft.WhenToChoose ?? string.Empty;
        Items = draft.Items.ToArray();
        IsOpen = true;
    }

    public MatchupItemBlockDraft CreateSnapshot(string id)
    {
        if (!CanSave)
        {
            throw new InvalidOperationException("Нельзя сохранить блок предметов с некорректным названием.");
        }

        var normalizedHint = WhenToChoose.Trim();
        return new MatchupItemBlockDraft(
            id,
            Name.Trim(),
            normalizedHint.Length == 0 ? null : normalizedHint,
            Items.ToArray());
    }

    public void SetItems(IEnumerable<ItemOptionViewModel> selectedItems)
    {
        ArgumentNullException.ThrowIfNull(selectedItems);
        var selected = MatchupItemDraftFactory.Create(selectedItems);
        var selectedById = selected.ToDictionary(static item => item.Id);
        Items =
        [
            .. Items
                .Where(item => selectedById.ContainsKey(item.Id))
                .Select(item => selectedById[item.Id]),
            .. selected.Where(selectedItem => Items.All(item => item.Id != selectedItem.Id))
        ];
    }

    public bool RemoveItem(MatchupItemDraft item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var updatedItems = Items.Where(existing => existing.Id != item.Id).ToArray();
        if (updatedItems.Length == Items.Count)
        {
            return false;
        }

        Items = updatedItems;
        return true;
    }

    public bool MoveItemLeft(MatchupItemDraft item) => MoveItem(item, -1);

    public bool MoveItemRight(MatchupItemDraft item) => MoveItem(item, 1);

    public void Close() => IsOpen = false;

    public void UpdateLanguage()
    {
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(NameLengthFeedback));
    }

    private bool MoveItem(MatchupItemDraft item, int offset)
    {
        ArgumentNullException.ThrowIfNull(item);
        var currentIndex = Items.ToList().FindIndex(existing => existing.Id == item.Id);
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= Items.Count)
        {
            return false;
        }

        var updatedItems = Items.ToList();
        updatedItems.RemoveAt(currentIndex);
        updatedItems.Insert(targetIndex, item);
        Items = updatedItems.ToArray();
        return true;
    }
}
